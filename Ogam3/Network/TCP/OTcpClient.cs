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
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.TCP;
using Ogam3.TxRx;
using Ogam3.Utils;

namespace Ogam3.Network.Tcp {
    public class OTcpClient : ISomeClient{
        public string Host;
        public int Port;

        public TcpClient ClientTcp;
        private Transfering _transfering;
        public uint BufferSize = 1048576;
        public NetworkStream Stream;
        private readonly Evaluator _evaluator;

        private readonly Synchronizer _connSync = new Synchronizer(true);
        private readonly Synchronizer _sendSync = new Synchronizer(true);

        private readonly IQueryInterface _serverQueryInterfaceProxy;
        private SymbolTable _symbolTable;
        private bool _isReconnectEnabled = true;

        public OTcpClient(string host, int port, Action connectionStabilised = null,  Evaluator evaluator = null) {
            Host = host;
            Port = port;
            ConnectionStabilised = connectionStabilised;

            if (evaluator == null) {
                _evaluator = new Evaluator();
            }

            _serverQueryInterfaceProxy = CreateProxy<IQueryInterface>();

            new Thread(() => {
                while (_isReconnectEnabled) {
                    ConnectServer();
                    _connSync.Wait();
                }
            }) {IsBackground = true}.Start();

            // enqueue sybmbol table call
            _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
        }

        public T CreateProxy<T>() {
            return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            ClassRegistrator.Register(_evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        private TcpClient ConnectTcp() {
            while (true) {
                try {
                    ClientTcp?.Close();
                    ClientTcp = new TcpClient();
                    ClientTcp.Connect(Host, Port);

                    break; // connection success
                }
                catch (Exception) {
                    ClientTcp?.Close();
                    Thread.Sleep(1000); // sleep reconnection
                }
            }

            return ClientTcp;
        }

        public Action ConnectionStabilised;
        
        public event Action<Exception> ConnectionError;

        private void ConnectServer() {
            var ns = new NetStream(ConnectTcp());

            _transfering?.Dispose();
            _transfering = new Transfering(ns, ns, BufferSize);

            var isReconnected = false;

            _transfering.ConnectionStabilised = OnConnectionStabilised + new Action(() => {
                if (isReconnected) {
                    _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
                }
                else {
                    isReconnected = true;
                }
            });

            _transfering.ConnectionError = ex => {
                lock (_transfering) {
                    // for single raction
                    _transfering.ConnectionError = null;

                    _sendSync.Lock();
                    Console.WriteLine($"Connection ERROR {ex.Message}");
                    OnConnectionError(ex);

                    _connSync.Pulse();
                }
            };

            _transfering.StartReceiver(data => OTcpServer.DataHandler(_evaluator, data, _symbolTable));

            _sendSync.Unlock();
        }

        public event Action<SpecialMessage, object> SpecialMessageEvt;

        protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
            SpecialMessageEvt?.Invoke(sm, call);
        }

        public object Call(object seq) {
            if (_sendSync.Wait(5000)) {
                var resp = BinFormater.Read(new MemoryStream(_transfering.Send(BinFormater.Write(seq, _symbolTable).ToArray())), _symbolTable);

                if (resp.Car() is SpecialMessage) {
                    OnSpecialMessageEvt(resp.Car() as SpecialMessage, seq);
                    return null;
                }

                return resp.Car();
            }
            else {
                // TODO connection was broken
                Console.WriteLine("Call error");
                OnConnectionError(new Exception("Call error"));
                return null;
            }
        }

        protected virtual void OnConnectionStabilised() {
            ConnectionStabilised?.Invoke();
        }

        protected virtual void OnConnectionError(Exception ex) {
            ConnectionError?.Invoke(ex);
        }

        public void Dispose() {
            _isReconnectEnabled = false;
            _transfering.Dispose();
            _sendSync.Dispose();
            _connSync.Dispose();
            ClientTcp.Close();
        }
    }
}
