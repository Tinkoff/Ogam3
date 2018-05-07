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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.TxRx;

namespace Ogam3.Network.Tcp {
    public class OTcpServer {
        private readonly TcpListener _listener;
        private Thread listerThread;
        public uint BufferSize = 1048576;
        public Evaluator Evaluator;

        public OTcpServer(int port, Evaluator evaluator = null) {
            Evaluator = evaluator ?? new Evaluator();

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            Evaluator.DefaultEnviroment.Define("get-context-tcp-client", new Func<dynamic>(() => ContexTcpClient));

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;
            listerThread.Start(_listener);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            ClassRegistrator.Register(Evaluator.DefaultEnviroment, instanceOfImplementation);
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

        public static byte[] DataHandler(Evaluator evl, byte[] data) {
            var receive = BinFormater.Read(new MemoryStream(data));

            try {
                var transactLog = new StringBuilder();
                transactLog.AppendLine($"<< {receive}");

                var res = evl.EvlSeq(receive);

                transactLog.AppendLine($">> {res}");
                Console.WriteLine(transactLog.ToString().Trim());

                if (res != null) {
                    return BinFormater.Write(res).ToArray();
                } else {
                    return new byte[0];
                }
            } catch (Exception e) {
                var ex = e;
                var sb = new StringBuilder();
                while (ex != null) {
                    sb.AppendLine(ex.Message);
                    ex = ex.InnerException;
                }
                Console.WriteLine(sb.ToString());
                return BinFormater.Write(new SpecialMessage(sb.ToString())).ToArray();
            }
        }

        private void ClientConnection(object o) {
            var client = (TcpClient) o;
            var endpoint = (IPEndPoint) client.Client.RemoteEndPoint;
            Console.WriteLine($"(client-connected \"{endpoint.Address}:{endpoint.Port}\")");

            var ns = new NetStream(client);

            var server = new Transfering(ns, ns, BufferSize);

            server.StartReceiver(data => {
                SetContextObj(ContextTcpClientId, client); // TODO single set
                SetContextObj(ReClientId, new ReClient(server, Evaluator)); // TODO single set

                return DataHandler(Evaluator, data);
            });
        }

        public class ReClient : ISomeClient {
            private Transfering _transfering;
            private Evaluator _evaluator;

            public event Action<Exception> ConnectionError;

            protected virtual void OnConnectionError(Exception ex) {
                ConnectionError?.Invoke(ex);
            }

            public ReClient(Transfering transfering, Evaluator _evaluator) {
                _transfering = transfering;
                _transfering.ConnectionError += OnConnectionError;
            }

            public T CreateProxy<T>() {
                return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
            }

            public object Call(object seq) {
                var resp = BinFormater.Read(new MemoryStream(_transfering.Send(BinFormater.Write(seq).ToArray())));

                return resp?.Car();
            }
        }
    }
}
