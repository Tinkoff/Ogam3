﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.TxRx;
using Ogam3.Utils;

namespace Ogam3.Network.Tcp {
    public class OTcpClient : ISomeClient{
        public string Host;
        public int Port;

        public TcpClient ClientTcp;
        private Transfering Transfering;
        private static int timeout = 60000;
        public uint BufferSize = 1048576;
        public NetworkStream Stream;
        private Evaluator Evaluator;

        private readonly Synchronizer _connSync = new Synchronizer(true);
        private readonly Synchronizer _sendSync = new Synchronizer(true);

        public OTcpClient(string host, int port, Evaluator evaluator = null) {
            Host = host;
            Port = port;

            if (evaluator == null) {
                Evaluator = new Evaluator();
            }

            new Thread(() => {
                while (true) {
                    ConnectServer();
                    _connSync.Wait();
                }
            }) {IsBackground = true}.Start();
        }

        public T CreateInterfase<T>() {
            return (T)Definer.CreateTcpCaller(typeof(T), this);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            Definer.Define(Evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        private TcpClient ConnectTcp() {
            while (true) {
                try {
                    ClientTcp?.Dispose();
                    ClientTcp = new TcpClient();
                    ClientTcp.Connect(Host, Port);

                    break; // connection success
                }
                catch (Exception e) {
                    ClientTcp?.Dispose();
                    Thread.Sleep(1000); // sleep reconnection
                }
            }

            return ClientTcp;
        }

        private void ConnectServer() {
            var ns = new NetStream(ConnectTcp());

            Transfering?.Dispose();
            Transfering = new Transfering(ns, ns, BufferSize);

            Transfering.ConnectionStabilised = () => { Console.WriteLine("Connection stabilised"); };

            Transfering.ConnectionError = ex => {
                lock (Transfering) {
                    // for single raction
                    Transfering.ConnectionError = null;

                    _sendSync.Lock();
                    Console.WriteLine($"Connection ERROR {ex.Message}");

                    _connSync.Pulse();
                }
            };

            Transfering.StartReceiver(data => { // TODO should be server implementation
                //Console.WriteLine($"Client receive {data.Length}Bt");
                //return new byte[0];
                var receive = BinFormater.Read(new MemoryStream(data));

                try {
                    var res = Evaluator.EvlSeq(receive);
                    if (res != null) {
                        return BinFormater.Write(res).ToArray();
                    } else {
                        return new byte[0];
                    }
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    return BinFormater.Write(new SpecialMessage(e)).ToArray();
                }
            });

            _sendSync.Unlock();
        }

        public event Action<SpecialMessage, object> SpecialMessageEvt;

        protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
            SpecialMessageEvt?.Invoke(sm, call);
        }

        public object Call(object seq) {
            if (_sendSync.Wait(5000)) {
                var resp = BinFormater.Read(new MemoryStream(Transfering.Send(BinFormater.Write(seq).ToArray()))).Car();
                if (resp is SpecialMessage) {
                    OnSpecialMessageEvt(resp as SpecialMessage, seq);
                    return null;
                }

                return resp;
            }
            else {
                // TODO connection was broken
                Console.WriteLine("Call error");
                return null;
            }
        }
    }
}
