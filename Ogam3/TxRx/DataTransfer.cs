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

using Ogam3.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ogam3.TxRx {
    public class DataTransfer {
        private Stream _sendStream;
        private Stream _receiveStream;
        private uint _quantSize;
        private readonly Dictionary<uint, DataBuilder> _pkgBuilder;
        private ConcurrentDictionary<ulong, Action<byte[]>> _synchronizer;

        private bool isTranferDead;
        private bool isConnectionStabilised;

        public static Action<string> Log = Console.WriteLine;

        public const ulong pingRap = 0;

        public DataTransfer(Stream sendStream, Stream receiveStream, uint quantSize) {
            _sendStream = sendStream;
            _receiveStream = receiveStream;
            _quantSize = quantSize;
            _pkgBuilder = new Dictionary<uint, DataBuilder>();
            _synchronizer = new ConcurrentDictionary<ulong, Action<byte[]>>();

            SetRapHandler(pingRap, (data) => {
                WriteData(new byte[0], pingRap); // ping accept
            });
        }

        public void SetRapHandler(ulong rap, Action<byte[]> handler) {
            _synchronizer[rap] = handler;
        }

        public event Action<ulong, byte[]> ReceivedData;

        public async Task StartReaderLoop() {
            try {
                while (true) {
                    await ReadData();
                }
            } catch (Exception e) {
                Log(e.Message);
                isTranferDead = true;
                OnConnectionError(e);
            }
        }

        public Action<Exception> ConnectionError;
        protected virtual void OnConnectionError(Exception ex) {
            isTranferDead = true;
            ConnectionError?.Invoke(ex);

            var zero = new byte[0];
            foreach (var handler in _synchronizer.Values.ToArray()) {
                try {
                    handler(zero); // brek transmitions
                } catch(Exception e) {
                    Log?.Invoke(e.ToString());
                }
            }
        }

        public Action ConnectionStabilised;
        protected virtual void OnTransferSuccess() {
            if (!isConnectionStabilised) {
                isConnectionStabilised = true;
                ConnectionStabilised?.Invoke();
            }
        }

        private async Task ReadData() {
            var tpLspS = await Package.ReadNextPakg(_receiveStream);
            if (tpLspS.IsQuantizied) {
                DataBuilder db = null;
                if (!_pkgBuilder.TryGetValue(tpLspS.QuantId, out db)) {
                    db = new DataBuilder(tpLspS.DataLength);
                    _pkgBuilder.Add(tpLspS.QuantId, db);
                }

                db.WriteData(tpLspS.QuantData, tpLspS.QuantShift);

                if (db.IsComplete()) {
                    var result = db.GetData();

                    _pkgBuilder.Remove(tpLspS.QuantId);
                    ReceivedData?.Invoke(tpLspS.Rap, result);
                }
            } else {
                var result = tpLspS.QuantData;
                ReceivedData?.Invoke(tpLspS.Rap, result);
            }
        }

        object _sendLocker = new object();
        public void WriteData(byte[] data, ulong rap) {
            if (isTranferDead) return;

            try {
                using (var sync = Stream.Synchronized(_sendStream)) {
                    foreach (var quant in Package.BuilPackages(data, _quantSize, rap)) {
                        lock (_sendLocker) {
                            sync.Write(quant, 0, quant.Length);
                        }
                    }
                }
                OnTransferSuccess();
            } catch (Exception e) {
                OnConnectionError(e);
            }
        }

        public bool HandlResp(ulong rap, byte[] data) {
            if(_synchronizer.TryGetValue(rap, out var handler)) {
                handler(data);
                return true;
            }

            return false;
        }

        public void SendAsync(byte[] data, Action<byte[]> callback) {
            if (isTranferDead)
                callback(new byte[0]);

            var rap = TpLspHelper.NewUID();
            _synchronizer.TryAdd(rap, (rslt) => {
                _synchronizer.TryRemove(rap, out var cb);
                callback(rslt);
            });

            WriteData(data, rap);
        }

        public byte[] Send(byte[] data) {
            if (isTranferDead)
                return new byte[0];

            var rap = TpLspHelper.NewUID();
            var sync = new Synchronizer(true);
            var result = new byte[0];

            _synchronizer.TryAdd(rap, (rslt) => {
                result = rslt;
                sync.Unlock();
            });

            WriteData(data, rap);

            if (!sync.Wait(TimeSpan.FromMinutes(15))) { // timeout 
                return new byte[0];
            }

            Action<byte[]> res;
            _synchronizer.TryRemove(rap, out res);
            return result;
        }


        class DataBuilder {
            private byte[] _data;
            private uint _writeCounter;
            public DataBuilder(uint dataLength) {
                _data = new byte[dataLength];
            }

            public DataBuilder WriteData(byte[] quant, uint shift) {
                Array.Copy(quant, 0, _data, shift, quant.Length);
                _writeCounter += (uint)quant.Length;
                return this;
            }

            public bool IsComplete() {
                return _data.Length == _writeCounter;
            }

            public byte[] GetData() {
                return _data;
            }
        }
    }
}
