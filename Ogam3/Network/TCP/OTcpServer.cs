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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.TCP;
using Ogam3.TxRx;

namespace Ogam3.Network.Tcp {
    public class OTcpServer {
        private readonly TcpListener _listener;
        private Thread listerThread;
        public uint BufferSize = 1048576;
        public Evaluator Evaluator;

        public static Action<string> Log = Console.WriteLine;

        private readonly QueryInterface _queryInterface;

        public OTcpServer(int port, Evaluator evaluator = null) {
            Evaluator = evaluator ?? new Evaluator();

            _queryInterface = new QueryInterface();
            _queryInterface.UpsertIndexedSymbols(new []{"quote", "lambda" , "if", "begin", "define", "set!", "call/cc", });
            _queryInterface.UpsertIndexedSymbols(Evaluator.DefaultEnviroment.Variables.Keys.ToArray());
            RegisterImplementation(_queryInterface);

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            Evaluator.DefaultEnviroment.Define("get-context-tcp-client", new Func<dynamic>(() => ContexTcpClient));

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;
            listerThread.Start(_listener);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            var symbols = ClassRegistrator.Register(Evaluator.DefaultEnviroment, instanceOfImplementation);
            _queryInterface.UpsertIndexedSymbols(symbols);
        }

        private void ListenerHandler(object o) {
            var listener = (TcpListener) o;
            while (true) {
                var client = listener.AcceptTcpClient();
                ClientConnection(client);
            }
        }

        private static string ContextTcpClientId = "context-tcp-client";
        private static string ReClientId = "context-re-client";

        public static object GetContextObj(string id) {
            return Thread.GetData(Thread.GetNamedDataSlot(id));
        }

        public static TcpClient ContexTcpClient => (TcpClient) GetContextObj(ContextTcpClientId);
        public static IPEndPoint ContextTcpEndPoint => (IPEndPoint)ContexTcpClient?.Client?.RemoteEndPoint;
        public static ReClient ContexReClient => (ReClient) GetContextObj(ReClientId);

        private static void SetContextObj(string id, object obj) {
            Thread.SetData(Thread.GetNamedDataSlot(id), obj);
        }

        public static byte[] DataHandler(Evaluator evl, byte[] data, SymbolTable symbolTable) {
            var transactLog = new StringBuilder();
            try {
                var receive = BinFormater.Read(new MemoryStream(data), symbolTable);

                transactLog.AppendLine($"<< {receive}");

                var res = evl.EvlSeq(receive);

                transactLog.AppendLine($">> {res}");
                Log?.Invoke(transactLog.ToString().Trim());

                if (res != null) {
                    return BinFormater.Write(res, symbolTable).ToArray();
                } else {
                    return new byte[0];
                }
            } catch (Exception e) {
                var ex = e;
                transactLog.AppendLine($">| {ex}");
                var sb = new StringBuilder();
                while (ex != null) {
                    sb.AppendLine(ex.Message);
                    ex = ex.InnerException;
                }
                Log?.Invoke(transactLog.ToString());
                return BinFormater.Write(new SpecialMessage(sb.ToString()), symbolTable).ToArray();
            }
        }

        private void ClientConnection(object o) {
            var client = (TcpClient) o;
            var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
            Log?.Invoke($"(client-connected \"{endpoint.Address}:{endpoint.Port}\")");

            var ns = new NetStream(client);

            var server = new Transfering(ns, ns, BufferSize);

            server.StartReceiver(data => {
                SetContextObj(ContextTcpClientId, client); // TODO single set
                SetContextObj(ReClientId, new ReClient(server, Evaluator, _queryInterface)); // TODO single set

                return DataHandler(Evaluator, data, _queryInterface.GetSymbolTable());
            });
        }

        public class ReClient : ISomeClient {
            public readonly Transfering Transfering;
            public readonly Evaluator Evaluator;

            public event Action<Exception> ConnectionError;

            private QueryInterface _queryInterface;

            protected virtual void OnConnectionError(Exception ex) {
                ConnectionError?.Invoke(ex);
            }

            public ReClient(Transfering transfering, Evaluator evaluator, QueryInterface queryInterface) {
                Transfering = transfering;
	            Evaluator = evaluator;
	            Transfering.ConnectionError += OnConnectionError;
                _queryInterface = queryInterface;
            }

            public T CreateProxy<T>() {
                return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
            }

            protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
                SpecialMessageEvt?.Invoke(sm, call);
            }

            public event Action<SpecialMessage, object> SpecialMessageEvt;

            public object Call(object seq) {
                var resp = BinFormater.Read(new MemoryStream(Transfering.Send(BinFormater.Write(seq, _queryInterface.GetSymbolTable()).ToArray())), _queryInterface.GetSymbolTable());

                if (resp.Car() is SpecialMessage) {
                    OnSpecialMessageEvt(resp.Car() as SpecialMessage, seq);
                    return null;
                }

                return resp?.Car();
            }
        }
    }
}
