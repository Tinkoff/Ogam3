﻿using Ogam3.Actors;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.Tcp;
using Ogam3.TxRx;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ogam3.Network.TCP {
    public class OTcpServer {
        private readonly TcpListener _listener;
        private Thread listerThread;
        public const uint BufferSize = 1048576;
        public Evaluator Evaluator;

        private static readonly Action<string> LogAction = Console.WriteLine;
        private readonly bool _isLogEnabled;

        private readonly QueryInterface _queryInterface;

        public OTActorEngine Actors;

        public OTcpServer(int port, Evaluator evaluator = null, bool isAutoStartListener = true, bool isLogEnabled = true) {
            _isLogEnabled = isLogEnabled;
            
            Evaluator = evaluator ?? new Evaluator();

            Actors = new OTActorEngine();

            _queryInterface = new QueryInterface();
            _queryInterface.UpsertIndexedSymbols(new[] { "quote", "lambda", "if", "begin", "define", "set!", "call/cc", });
            _queryInterface.UpsertIndexedSymbols(Evaluator.DefaultEnviroment.Variables.Keys.ToArray());
            RegisterImplementation(_queryInterface);

            _listener = new TcpListener(IPAddress.Any, port);

            Evaluator.DefaultEnviroment.Define("get-context-tcp-client", new Func<dynamic>(() => ContexTcpClient));

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;

            if (isAutoStartListener) {
                StartListener();
            }
        }

        public void StartListener() {
            _listener.Start();
            listerThread.Start(_listener);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            var symbols = ClassRegistrator.Register(Evaluator.DefaultEnviroment, instanceOfImplementation);
            _queryInterface.UpsertIndexedSymbols(symbols);
        }

        private async void ListenerHandler(object o) {
            //var stx = new SingleThreadedSynchronizationContext();

            //// event loop
            //new Thread(() => { stx.StartEventLoop(); }) { IsBackground = true }.Start();

            var listener = (TcpListener)o;
            while (true) {
                var tcpClient = await listener.AcceptTcpClientAsync();
                ClientConnected(tcpClient);
            }

            _listener.Stop();
        }

        private void ClientConnected(TcpClient client) {
            var ns = new NetStream(client);
            var server = new DataTransfer(ns, ns, BufferSize);
            server.ConnectionClose += () => {
                client.Close();
            };

            server.ReceivedData += (rap, data) => {
                //Console.WriteLine("Main Thread: {0}", Thread.CurrentThread.ManagedThreadId);
                var otContext = new OTContext() { // Handle in other thread
                    Context = rap,
                    Data = data,
                    TcpClient = client,
                    ReClient = new ReClient(server, Evaluator, _queryInterface),
                    Evaluator = Evaluator,
                    DataTransfer = server
                };
                if (_isLogEnabled) {
                    otContext.Callback = HandleRequestLogging;    
                }
                else {
                    otContext.Callback = HandleRequest;    
                }
                Actors.EnqueueData(otContext);
            };

            server.StartReaderLoop();
        }

        private static void HandleRequest(OTContext context) {
            if (context.DataTransfer.HandlResp(context.Context, context.Data)) {
                // handled as result of request
                return;
            }

            SetContextObj(ContextId, context);

            var response = new byte[0];
            try {
                var receive = BinFormater.Read(new MemoryStream(context.Data), context.SymbolTable);
                var res = context.Evaluator.EvlSeq(receive, false);
                if (res != null) {
                    response = BinFormater.Write(res, context.SymbolTable).ToArray();
                }
            } catch (Exception e) {
                var ex = e;
                var sb = new StringBuilder();
                while (ex != null) {
                    sb.AppendLine(ex.Message);
                    ex = ex.InnerException;
                }
                var msg = sb.ToString();
                LogAction?.Invoke(msg);
                response = BinFormater.Write(new SpecialMessage(msg), context.SymbolTable).ToArray();
            }

            // send response to client
            context.DataTransfer.WriteData(response, context.Context);
        }

        private static void HandleRequestLogging(OTContext context) {
            if (context.DataTransfer.HandlResp(context.Context, context.Data)) {
                // handled as result of request
                return;
            }

            SetContextObj(ContextId, context);

            var responce = new byte[0];

            var transactLog = new StringBuilder();
            try {
                var receive = BinFormater.Read(new MemoryStream(context.Data), context.SymbolTable);

                transactLog.AppendLine($"<< {receive}");

                var res = context.Evaluator.EvlSeq(receive, false);

                transactLog.AppendLine($">> {res}");
                LogAction?.Invoke(transactLog.ToString().Trim());

                if (res != null) {
                    responce = BinFormater.Write(res, context.SymbolTable).ToArray();
                }
            } catch (Exception e) {
                var ex = e;
                transactLog.AppendLine($">| {ex}");
                var sb = new StringBuilder();
                while (ex != null) {
                    sb.AppendLine(ex.Message);
                    ex = ex.InnerException;
                }
                LogAction?.Invoke(transactLog.ToString());
                responce = BinFormater.Write(new SpecialMessage(sb.ToString()), context.SymbolTable).ToArray();
            }

            // send responce to client
            context.DataTransfer.WriteData(responce, context.Context);
        }


        private static object GetContextObj(string id) {
            return Thread.GetData(Thread.GetNamedDataSlot(id));
        }

        private static void SetContextObj(string id, object obj) {
            Thread.SetData(Thread.GetNamedDataSlot(id), obj);
        }

        public static string ContextId = "o-tcp-context";

        public static OTContext Contex => (OTContext)GetContextObj(ContextId);
        public static TcpClient ContexTcpClient => Contex.TcpClient;
        public static IPEndPoint ContextTcpEndPoint => (IPEndPoint)ContexTcpClient?.Client?.RemoteEndPoint;
        public static ReClient ContexReClient => Contex.ReClient;

        public class ReClient : ISomeClient {
            public readonly DataTransfer DataTransfer;
            public readonly Evaluator Evaluator;

            public event Action<Exception> ConnectionError;

            private QueryInterface _queryInterface;

            protected virtual void OnConnectionError(Exception ex) {
                ConnectionError?.Invoke(ex);
            }

            public ReClient(DataTransfer dataTransfer, Evaluator evaluator, QueryInterface queryInterface) {
                DataTransfer = dataTransfer;
                Evaluator = evaluator;
                DataTransfer.ConnectionError += OnConnectionError;
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
                var resp = BinFormater.Read(new MemoryStream(DataTransfer.Send(BinFormater.Write(seq, _queryInterface.GetSymbolTable()).ToArray())), _queryInterface.GetSymbolTable());

                if (resp.Car() is SpecialMessage) {
                    OnSpecialMessageEvt(resp.Car() as SpecialMessage, seq);
                    return null;
                }

                return resp?.Car();
            }

            public Async<object> AsyncCall(object seq) {
                throw new NotImplementedException();
            }

            public void Close() {
                DataTransfer.Close();
            }
        }
    }
}
