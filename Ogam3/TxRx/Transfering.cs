using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ogam3.TxRx {
    class Transfering {
        private Stream _sendStream;
        private Stream _receiveStream;
        private uint _quantSize;
        private Dictionary<ulong, Action<byte[]>> _synchronizer;

        public Transfering(Stream sendStream, Stream receiveStream, uint quantSize) {
            _sendStream = sendStream;
            _receiveStream = receiveStream;
            _quantSize = quantSize;
            _synchronizer = new Dictionary<ulong, Action<byte[]>>();

            Task.Factory.StartNew(() => {
                var rap = 0;
                var sw = new Stopwatch();
                while (true) {
                    var sem = new SemaphoreSlim(0, 1);
                    _synchronizer[0] = (rslt) => {
                        if (sem.CurrentCount == 0) {
                            sem.Release();
                        }
                    };

                    SendManager(new byte[0], 0);

                    sem.Wait();
                    sw.Restart();
                    _synchronizer.Remove(0);
                    sw.Stop();
                    Console.WriteLine($"Ping accept {sw.Elapsed}");
                    Thread.Sleep(60000);
                }
            });
        }

        public byte[] Send(byte[] data) {
            var rap = TpLspHelper.NewUID();
            var sem = new SemaphoreSlim(0,1);
            byte[] result = new byte[0];
            _synchronizer.Add(rap, (rslt) => {
                result = rslt;
                sem.Release();
            });

            SendManager(data, rap);

            sem.Wait();
            _synchronizer.Remove(rap);
            return result;
        }

        private void SendManager(byte[] data, ulong rap) {
            using (var sync = Stream.Synchronized(_sendStream)) {
                foreach (var quant in TpLspHelper.Quantize(data, _quantSize, rap)) {
                    sync.Write(quant, 0, quant.Length);
                }
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
                                    var speed = result.Length / 1024.0 / 1024.0 * (1000.0 / ((double) w.ElapsedMilliseconds / cnt));

                                    Console.WriteLine($"[{cnt++}]Received {result.Length} bytes {speed:000.000}MB/Sec");

                                    receiveDataSet.BeginInvoke(tpLspS.Rap, result, null, null);
                                }

                            }
                            else {
                                var result = tpLspS.QuantData;
                                var speed = result.Length / 1024.0 / 1024.0 * (1000.0 / ((double) w.ElapsedMilliseconds / cnt));
                                Console.WriteLine($"[{cnt++}]Received {result.Length} bytes {speed:000.000}MB/Sec");

                                receiveDataSet.BeginInvoke(tpLspS.Rap, result, null, null);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("TODO make reconnect");
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
    }
}
