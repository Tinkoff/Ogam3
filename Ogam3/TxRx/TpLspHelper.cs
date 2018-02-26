using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using LZ4;

namespace Ogam3.TxRx {
    class TpLspHelper {
        private static byte[] Coding(byte[] data) {
            //return Escape(Compress(data));
            return Compress(data);
            //return Escape(data);
            //return data;
        }

        private static byte[] unCoding(byte[] data) {
            //return Decompress(UnEscape(data));
            return Decompress(data);
            //return UnEscape(data);
            //return data;
        }

        public static void SendData(byte[] data, Stream channel, uint quantSize, ulong rap) {
            using (var sync = Stream.Synchronized(channel)) {
                foreach (var quant in Quantize(data, quantSize, rap)) {
                    sync.Write(quant, 0, quant.Length);
                }
            }
        }

        public static IEnumerable<byte[]> Quantize(byte[] data, uint quantSize, ulong rap) {
            return MakeQuants(data, quantSize, rap);
        }

        public static ulong NewUID() {
            return BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        }

        private static IEnumerable<byte[]> MakeQuants(byte[] data, uint quantSize, ulong rap) {
            var wholeQuantCount = (uint) data.Length / quantSize;
            var tailByteCount = (uint) data.Length % quantSize;
            var totalQuantCount = wholeQuantCount + (uint) (tailByteCount > 0 ? 1 : 0);
            var quantId = (uint) (DateTime.Now.Ticks - DateTime.Today.Ticks);

            for (uint i = 0; i < wholeQuantCount; i++) {
                yield return BuildPackageX(data, rap, i * quantSize, quantSize, quantId);
            }

            if (tailByteCount > 0) {
                yield return BuildPackageX(data, rap, wholeQuantCount * quantSize, tailByteCount, quantId);
            } else if (wholeQuantCount == 0) {
                yield return BuildPackageX(data, rap, 0, (uint)data.Length, quantId);
            }

        }

        private static byte[] CutDataQuant(byte[] data, uint start, uint length) {
            if (start == 0 && data.Length == length) return data; // speedup

            var qnt = new byte[length];
            Array.Copy(data, start, qnt, 0, length);
            return qnt;
        }

        private static byte CalcCheckSumm(IEnumerable<byte> seq) {
            return seq.Aggregate((acc, itm) => (byte) (acc ^ itm));
        }

        private static byte[] BuildPackageX(byte[] data, ulong rap, uint shift, uint quantSize, uint quantId) {
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
            var escaped = Coding(CutDataQuant(data, shift, quantSize));
            pkg.AddRange(BitConverter.GetBytes(escaped.Length));
            pkg.AddRange(escaped);

            return pkg.ToArray();
        }

        private static byte[] Escape(byte[] data) {
            var res = new List<byte>();
            foreach (var b in data) {
                res.Add(b);
                if (b == TpLspS.BEGIN) {
                    res.Add(TpLspS.ESCAPE);
                }
            }

            return res.ToArray();
        }

        private static byte[] UnEscape(byte[] data) {
            var res = new List<byte>();
            var isEsc = false;
            foreach (var b in data) {
                if (isEsc) {
                    isEsc = false;
                }
                else {
                    res.Add(b);
                }
                if (b == TpLspS.BEGIN) {
                    isEsc = true;
                }
            }

            return res.ToArray();
        }

        static byte[] Compress(byte[] data) {
            using (var compressedStream = new MemoryStream()) {
                using (var zipStream = new LZ4Stream(compressedStream, CompressionMode.Compress)) {
// GZipStream DeflateStream
                    zipStream.Write(data, 0, data.Length);
                    zipStream.Close();
                    return compressedStream.ToArray();
                }
            }
        }

        static byte[] Decompress(byte[] data) {
            using (var compressedStream = new MemoryStream(data)) {
                using (var zipStream = new LZ4Stream(compressedStream, CompressionMode.Decompress)) {
                    using (var resultStream = new MemoryStream()) {
                        zipStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
        }

        public static IEnumerable<TpLspS> SequenceReader(Stream stream) {
            while (true) {
                var pkg = ReadNextPakg(stream);

                if (pkg == null) break;

                yield return pkg.Value;
            }
        }

        private static TpLspS? ReadNextPakg(Stream stream) {
            if (!stream.CanRead) return null;

            if (stream is NetworkStream) {
                if (!(stream as NetworkStream).DataAvailable) return null;
            }

            var pkg = new TpLspS();

            while (true) { // find begin of pkg
                var bt = stream.ReadByte();
                if (bt == TpLspS.BEGIN) {
                    var seq = new List<byte>();
                    seq.Add(TpLspS.BEGIN);
                    var arr = new byte[sizeof(ulong)];
                    var realSize = stream.Read(arr, 0, arr.Length);
                    if (realSize != arr.Length) return null;
                    pkg.Rap = BitConverter.ToUInt64(arr, 0);

                    seq.AddRange(arr);
                    if (CalcCheckSumm(seq) != stream.ReadByte()) {
                        return null;
                    }

                    break;
                }
                if (bt == -1) return null;
            }

            while (true) {
                var bt = stream.ReadByte();

                if (bt == -1) return null;

                if (bt == TpLspS.PKGCOUNT) {
                    var size = sizeof(uint) + sizeof(uint) + sizeof(uint);
                    var arr = new byte[size];
                    var realSize = stream.Read(arr, 0, arr.Length);

                    if (realSize != arr.Length) return null;

                    pkg.DataLength = BitConverter.ToUInt32(arr, 0);
                    pkg.QuantShift = BitConverter.ToUInt32(arr, sizeof(uint));
                    pkg.QuantId = BitConverter.ToUInt32(arr, sizeof(uint) + sizeof(uint));
                    pkg.IsQuantizied = true;
                }
                else if (bt == TpLspS.DATA) {
                    var arr = new byte[sizeof(uint)];


                    var realSize = stream.Read(arr, 0, arr.Length);

                    if (realSize != arr.Length) return null;

                    pkg.QuantData = new byte[BitConverter.ToUInt32(arr, 0)];


                    if (pkg.QuantData.Length > 0) {
                        var dataPointer = 0;
                        while (dataPointer < pkg.QuantData.Length) {
                            realSize = stream.Read(pkg.QuantData, dataPointer, pkg.QuantData.Length - dataPointer);

                            if (realSize <= 0) return null;

                            dataPointer += realSize;
                        }

                        pkg.QuantData = unCoding(pkg.QuantData);
                    }

                    break;
                }
            }

            return pkg;
        }
    }
}