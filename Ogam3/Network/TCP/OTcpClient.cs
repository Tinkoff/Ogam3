using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.Tcp;
using Ogam3.TxRx;
using Ogam3.Utils;
using Ogam3.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace Ogam3.Network.TCP {
    public class OTcpClient : ISomeClient, IDisposable {
        public string Host;
        public int Port;

        public TcpClient ClientTcp;
        private DataTransfer _dataTransfer;
        public NetworkStream Stream;
        private readonly Evaluator _evaluator;

        public static Action<string> Log = Console.WriteLine;

        private readonly Synchronizer _sendSync = new Synchronizer(true);

        private readonly IQueryInterface _serverQueryInterfaceProxy;
        private SymbolTable _symbolTable;
        private bool _isKeepConnection = true;

        public Action ConnectionStabilised;

        public event Action<Exception> ConnectionError;

        public OTActorEngine Actors;

        public bool IsConnected { get; private set; }

        public OTcpClient(string host, int port, Action connectionStabilised = null, Evaluator evaluator = null) {
            Host = host;
            Port = port;
            ConnectionStabilised = connectionStabilised;

            if (evaluator == null) {
                _evaluator = new Evaluator();
            }

            Actors = new OTActorEngine();

            _serverQueryInterfaceProxy = CreateProxy<IQueryInterface>();

            new Thread(() => {
                while (_isKeepConnection) {
                    if (IsConnected) {
                        _dataTransfer.WriteData(new byte[0], DataTransfer.pingRap);
                        Thread.Sleep(15000);
                    } else {
                        ConnectServer();
                        IsConnected = true;
                    }
                }
            }) { IsBackground = true }.Start();

            // enqueue sybmbol table call
            _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
        }

        public T CreateProxy<T>() {
            return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
        }

        public event Action<SpecialMessage, object> SpecialMessageEvt;

        protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
            SpecialMessageEvt?.Invoke(sm, call);
        }

        private void ConnectServer() {
            var ns = new NetStream(ConnectTcp());

            _dataTransfer = new DataTransfer(ns, ns, OTcpServer.BufferSize);

            var isReconnected = false;

            _dataTransfer.SetRapHandler(DataTransfer.pingRap, (data) => {
                // apply ping
            });

            _dataTransfer.ConnectionStabilised = new Action(() => {
                if (isReconnected) {
                    _symbolTable = null;
                    _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
                } else {
                    isReconnected = true;
                } 
            }) + OnConnectionStabilised;

            _dataTransfer.ConnectionError = ex => {
                lock (_dataTransfer) {
                    // for single raction
                    _dataTransfer.ConnectionError = null;

                    _sendSync.Lock();
                    Log?.Invoke($"Connection ERROR {ex.Message}");
                    OnConnectionError(ex);

                    IsConnected = false;
                }
            };

            _dataTransfer.ReceivedData += (rap, data) => {
                Actors.EnqueueData(new OTContext() { // Handle in other thread
                    Context = rap,
                    Data = data,
                    TcpClient = ClientTcp,
                    Evaluator = _evaluator,
                    DataTransfer = _dataTransfer,
                    Callback = HandleRequest
                });
            };

            _dataTransfer.StartReaderLoop();


            _sendSync.Unlock();
        }

        private static void HandleRequest(OTContext context) {
            if (context.DataTransfer.HandlResp(context.Context, context.Data)) {
                // handled as result of request
                return;
            }

            var responce = new byte[0];

            var transactLog = new StringBuilder();
            try {
                var receive = BinFormater.Read(new MemoryStream(context.Data), context.SymbolTable);

                transactLog.AppendLine($"<< {receive}");

                var res = context.Evaluator.EvlSeq(receive, false);

                transactLog.AppendLine($">> {res}");
                Log?.Invoke(transactLog.ToString().Trim());

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
                Log?.Invoke(transactLog.ToString());
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

        protected virtual void OnConnectionStabilised() {
            ConnectionStabilised?.Invoke();
        }

        protected virtual void OnConnectionError(Exception ex) {
            ConnectionError?.Invoke(ex);
        }

        private TcpClient ConnectTcp() {
            while (true) {
                try {
                    ClientTcp?.Close();
                    ClientTcp = new TcpClient();
                    ClientTcp.Connect(Host, Port);

                    break; // connection success
                } catch (Exception) {
                    ClientTcp?.Close();
                    Thread.Sleep(1000); // sleep reconnection
                }
            }

            return ClientTcp;
        }

        public object Call(object seq) {
            if (_sendSync.Wait(5000)) {
                var resp = BinFormater.Read(new MemoryStream(_dataTransfer.Send(BinFormater.Write(seq, _symbolTable).ToArray())), _symbolTable);

                if (resp.Car() is SpecialMessage) {
                    OnSpecialMessageEvt(resp.Car() as SpecialMessage, seq);
                    return null;
                }

                return resp.Car();
            } else {
                // TODO connection was broken
                Console.WriteLine("Call error");
                OnConnectionError(new Exception("Call error"));
                return null;
            }
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            ClassRegistrator.Register(_evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        public void Dispose() {
            _isKeepConnection = false;
           // _transfering?.Dispose();
            _sendSync?.Dispose();
            //_connSync?.Dispose();
            ClientTcp?.Close();
        }
    }
}
