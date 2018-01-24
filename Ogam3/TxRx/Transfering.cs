using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ogam3.Utils;

namespace Ogam3.TxRx {
    class Transfering : IDisposable {
        private Stream _sendStream;
        private Stream _receiveStream;
        private uint _quantSize;
        private Dictionary<ulong, Action<byte[]>> _synchronizer;

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
            _synchronizer = new Dictionary<ulong, Action<byte[]>>();

            Task.Factory.StartNew(() => {
                var rap = 0;
                var pingRap = TpLspHelper.NewUID();
                while (true) {
                    var sync = new Synchronizer(true);
                    _synchronizer[pingRap] = (rslt) => {
                        sync.Unlock();
                    };

                    SendManager(new byte[0], 0);

                    if (!sync.Wait(1000)) {
                        isConnectionStabilised = false;
                        return;
                    }

                    _synchronizer.Remove(pingRap);
                    Thread.Sleep(11000);
                }
            });
        }

        public byte[] Send(byte[] data) {
            if (isTranferDead) return new byte[0];

            var rap = TpLspHelper.NewUID();
            var sync = new Synchronizer(true);
            var result = new byte[0];

            _synchronizer.Add(rap, (rslt) => {
                result = rslt;
                sync.Unlock();
            });

            SendManager(data, rap);

            sync.Wait();
            _synchronizer.Remove(rap);
            return result;
        }

        private void SendManager(byte[] data, ulong rap) {
            try { 
                using (var sync = Stream.Synchronized(_sendStream)) {
                    foreach (var quant in TpLspHelper.Quantize(data, _quantSize, rap)) {
                        sync.Write(quant, 0, quant.Length);
                    }
                }
                OnTransferSuccess();
            }
            catch (Exception e) {
                OnConnectionError(e);
            }
        }

        public void StartReceiver(Func<byte[], byte[]> requestHandler) {
            StartListener(_receiveStream, (rap, data) => {
                Action<byte[]> receiveAct = null;
                if (_synchronizer.TryGetValue(rap, out receiveAct)) {
                    receiveAct(data);
                }
                else {
                    requestHandler.BeginInvoke(data, ar => {
                        var result = requestHandler.EndInvoke(ar);
                        SendManager(result, rap);
                    }, null);
                }
            });
        }

        Thread StartListener(Stream transferChannel, Action<ulong, byte[]> receiveDataSet) {
            var listenThrd = new Thread(() => { // Listener thread
                try { 
                    var pkgBuilder = new Dictionary<uint, DataBuilder>();
                    var cnt = 1;
                    var w = new Stopwatch();
                    w.Start();
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
                                    //var speed = result.Length / 1024.0 / 1024.0 * (1000.0 / ((double) w.ElapsedMilliseconds / cnt));

                                    //Console.WriteLine($"[{cnt++}]Received {result.Length} bytes {speed:000.000}MB/Sec");

                                    receiveDataSet.BeginInvoke(tpLspS.Rap, result, null, null);
                                }

                            }
                            else {
                                var result = tpLspS.QuantData;
                                //var speed = result.Length / 1024.0 / 1024.0 * (1000.0 / ((double) w.ElapsedMilliseconds / cnt));
                                //Console.WriteLine($"[{cnt++}]Received {result.Length} bytes {speed:000.000}MB/Sec");

                                receiveDataSet.BeginInvoke(tpLspS.Rap, result, null, null);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    OnConnectionError(e);
                }
            }) {IsBackground = true};

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
            ConnectionStabilised = null;
            ConnectionError = null;
        }
    }
}
