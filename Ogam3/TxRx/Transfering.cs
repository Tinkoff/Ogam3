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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ogam3.Utils;
using Action = System.Action;

namespace Ogam3.TxRx {
    public class Transfering : IDisposable {
        private Stream _sendStream;
        private Stream _receiveStream;
        private uint _quantSize;
        private ConcurrentDictionary<ulong, Action<byte[]>> _synchronizer;

        private bool isTranferDead;

        private bool isConnectionStabilised;
        public Action ConnectionStabilised;
        protected virtual void OnTransferSuccess() {
            if (!isConnectionStabilised) {
                isConnectionStabilised = true;
                ConnectionStabilised?.Invoke();
            }
        }

        public Action<Exception> ConnectionError;
        protected virtual void OnConnectionError(Exception ex) {
            isTranferDead = true;
            ConnectionError?.Invoke(ex);
        }

        public Transfering(Stream sendStream, Stream receiveStream, uint quantSize) {
            _sendStream = sendStream;
            _receiveStream = receiveStream;
            _quantSize = quantSize;
            _synchronizer = new ConcurrentDictionary<ulong, Action<byte[]>>();

            var pingRap = (ulong)0;

            var sync = new Synchronizer(true);
            _synchronizer[pingRap] = (rslt) => {
                sync.Unlock();
            };

            new Thread(() => {
                while (true) {
                    sync.Lock();
                    SendManager(new byte[0], 0);

                    if (!sync.Wait(25000)) {
                        isConnectionStabilised = false;
                        Console.WriteLine("PING TIMEOUT ON CURRENT CONNECTION");
                    }

                    if (isTranferDead) {
                        FreeBazukaPool();
                        return; // kill current thread
                    }

                    Thread.Sleep(11000);
                }
            }) { IsBackground = true, Priority = ThreadPriority.AboveNormal }.Start();
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

            SendManager(data, rap);

            sync.Wait();
            Action<byte[]> res;
            _synchronizer.TryRemove(rap, out res);
            return result;
        }

        static object _sendLocker = new object();

        private void SendManager(byte[] data, ulong rap) {
            try {
                using (var sync = Stream.Synchronized(_sendStream)) {
                    foreach (var quant in TpLspHelper.Quantize(data, _quantSize, rap)) {
                        lock (_sendLocker) {
                            sync.Write(quant, 0, quant.Length);
                        }
                    }
                }
                OnTransferSuccess();
            }
            catch (Exception e) {
                OnConnectionError(e);
            }
        }

        public void StartReceiver(System.Func<byte[], byte[]> requestHandler) {
            StartListener(_receiveStream, (rap, data) => {
                Action<byte[]> receiveAct = null;
                if (_synchronizer.TryGetValue(rap, out receiveAct)) {
                    _respBazuka.Shot(() => {
                        receiveAct(data);
                    });
                }
                else {
                    _reqBazuka.Shot(() => {
                        SendManager(requestHandler(data), rap);
                    });
                }
            });
        }

        private readonly ThreadBazuka _reqBazuka = new ThreadBazuka();
        private readonly ThreadBazuka _respBazuka = new ThreadBazuka();

        private void FreeBazukaPool() {
            _reqBazuka.Dispose();
            _respBazuka.Dispose();
        }

        Thread StartListener(Stream transferChannel, System.Action<ulong, byte[]> receiveDataSet) {
            var listenThrd = new Thread(() => { // Listener thread
                try { 
                    var pkgBuilder = new Dictionary<uint, DataBuilder>();
                    while (true) {
                        foreach (var tpLspS in TpLspHelper.SequenceReader(transferChannel)) {
                            if (tpLspS.IsQuantizied) {
                                DataBuilder db = null;
                                if (!pkgBuilder.TryGetValue(tpLspS.QuantId, out db)) {
                                    db = new DataBuilder(tpLspS.DataLength);
                                    pkgBuilder.Add(tpLspS.QuantId, db);
                                }

                                db.WriteData(tpLspS.QuantData, tpLspS.QuantShift);

                                if (db.IsComplete()) {
                                    var result = db.GetData();

                                    pkgBuilder.Remove(tpLspS.QuantId);
                                    receiveDataSet.Invoke(tpLspS.Rap, result);
                                }

                            }
                            else {
                                var result = tpLspS.QuantData;
                                receiveDataSet.Invoke(tpLspS.Rap, result);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    isTranferDead = true;
                    OnConnectionError(e);
                }
            }) {IsBackground = true, Priority = ThreadPriority.Normal};

            listenThrd.Start();

            return listenThrd;
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

        public void Dispose() {
            _sendStream?.Dispose();
            _receiveStream?.Dispose();
            FreeBazukaPool();
            ConnectionStabilised = null;
            ConnectionError = null;
            isTranferDead = true;
        }
    }
}
