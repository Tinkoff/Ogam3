/*
 * Copyright © 2018 Tinkoff Bank
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Ogam3.TxRx {
    public class Package {

        public static IEnumerable<byte[]> BuilPackages(byte[] data, uint quantSize, ulong rap) {
            var wholeQuantCount = (uint)data.Length / quantSize;
            var tailByteCount = (uint)data.Length % quantSize;
            var quantId = (uint)(DateTime.Now.Ticks - DateTime.Today.Ticks);

            for (uint i = 0; i < wholeQuantCount; i++) {
                yield return BuildPack(data, rap, i * quantSize, quantSize, quantId);
            }

            if (tailByteCount > 0) {
                yield return BuildPack(data, rap, wholeQuantCount * quantSize, tailByteCount, quantId);
            } else if (wholeQuantCount == 0) {
                yield return BuildPack(data, rap, 0, (uint)data.Length, quantId);
            }
        }

        private static byte[] BuildPack(byte[] data, ulong rap, uint shift, uint quantSize, uint quantId) {
            var pkg = new List<byte>();
            pkg.Add(TpLspS.BEGIN);
            pkg.AddRange(BitConverter.GetBytes(rap));

            pkg.Add(CalcCheckSumm(pkg));

            if (data.Length > quantSize) {
                pkg.Add(TpLspS.PKGCOUNT);
                pkg.AddRange(BitConverter.GetBytes(data.Length));
                pkg.AddRange(BitConverter.GetBytes(shift));
                pkg.AddRange(BitConverter.GetBytes(quantId));
            }

            pkg.Add(TpLspS.DATA);
            var escaped = CutDataQuant(data, shift, quantSize);
            pkg.AddRange(BitConverter.GetBytes(escaped.Length));
            pkg.AddRange(escaped);

            return pkg.ToArray();
        }

        private static byte[] CutDataQuant(byte[] data, uint start, uint length) {
            if (start == 0 && data.Length == length) return data; // speedup

            var qnt = new byte[length];
            Array.Copy(data, start, qnt, 0, length);
            return qnt;
        }

        public static async Task<TpLspS> ReadNextPakg(Stream stream) {
            if (!stream.CanRead) return null;

            if (stream is NetworkStream) {
                if (!(stream as NetworkStream).DataAvailable) return null;
            }

            var pkg = new TpLspS();

            while (true) { // find begin of pkg
                var bt = await ReadByte(stream);
                if (bt == TpLspS.BEGIN) {
                    var seq = new List<byte>();
                    seq.Add(TpLspS.BEGIN);
                    var arr = new byte[sizeof(ulong)];
                    var realSize = await stream.ReadAsync(arr, 0, arr.Length);
                    if (realSize != arr.Length) return null;
                    pkg.Rap = BitConverter.ToUInt64(arr, 0);

                    seq.AddRange(arr);
                    if (CalcCheckSumm(seq) != await ReadByte(stream)) {
                        return null;
                    }

                    break;
                }
                if (bt == -1) return null;
            }

            while (true) {
                var bt = await ReadByte(stream);

                if (bt == -1) return null;

                if (bt == TpLspS.PKGCOUNT) {
                    var size = sizeof(uint) + sizeof(uint) + sizeof(uint);
                    var arr = new byte[size];
                    var realSize = await stream.ReadAsync(arr, 0, arr.Length);

                    if (realSize != arr.Length) return null;

                    pkg.DataLength = BitConverter.ToUInt32(arr, 0);
                    pkg.QuantShift = BitConverter.ToUInt32(arr, sizeof(uint));
                    pkg.QuantId = BitConverter.ToUInt32(arr, sizeof(uint) + sizeof(uint));
                    pkg.IsQuantizied = true;
                } else if (bt == TpLspS.DATA) {
                    var arr = new byte[sizeof(uint)];


                    var realSize = await stream.ReadAsync(arr, 0, arr.Length);

                    if (realSize != arr.Length) return null;

                    pkg.QuantData = new byte[BitConverter.ToUInt32(arr, 0)];


                    if (pkg.QuantData.Length > 0) {
                        var dataPointer = 0;
                        while (dataPointer < pkg.QuantData.Length) {
                            realSize = await stream.ReadAsync(pkg.QuantData, dataPointer, pkg.QuantData.Length - dataPointer);

                            if (realSize <= 0) return null;

                            dataPointer += realSize;
                        }

                        //pkg.QuantData = unCoding(pkg.QuantData);
                    }

                    break;
                }
            }

            return pkg;
        }

        private static async Task<int> ReadByte(Stream s) {
            var buff = new byte[1];
            if (await s.ReadAsync(buff, 0, 1) == 1) {
                return buff[0];
            }

            return -1;
        }

        private static byte CalcCheckSumm(IEnumerable<byte> seq) {
            return seq.Aggregate((acc, itm) => (byte)(acc ^ itm));
        }
    }
}
