using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.Tcp;
using Ogam3.Network.TCP;
using Ogam3.TxRx;
using Ogam3.Utils;

namespace Ogam3.Network.Pipe {
    public class OPipeClient : ISomeClient {
        public uint BufferSize = 65535;

        private readonly Evaluator _evaluator;

        public Action ConnectionStabilised;
        public event Action<Exception> ConnectionError;
        public event Action<SpecialMessage, object> SpecialMessageEvt;

        private readonly IQueryInterface _serverQueryInterfaceProxy;
        private SymbolTable _symbolTable;

        private readonly Synchronizer _connSync = new Synchronizer(true);
        private readonly Synchronizer _sendSync = new Synchronizer(true);

        private Transfering _transfering;

        private string _pipeName;

        public OPipeClient(string pipeName, Action connectionStabilised = null, Evaluator evaluator = null) {
            _pipeName = pipeName;
            ConnectionStabilised = connectionStabilised;

            if (evaluator == null) {
                _evaluator = new Evaluator();
            }

            _serverQueryInterfaceProxy = CreateProxy<IQueryInterface>();

            new Thread(() => {
                while (true) {
                    ConnectPipeServer();
                    _connSync.Wait();
                    Thread.Sleep(3000); // Wait when OS close pipe if a pipe server was closed
                }
            }) { IsBackground = true }.Start();

            // enqueue sybmbol table call
            _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
        }

        public object Call(object seq) {
            if (_sendSync.Wait(5000)) {
                var resp = BinFormater.Read(new MemoryStream(_transfering.Send(BinFormater.Write(seq, _symbolTable).ToArray())), _symbolTable);

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

        private PipeClient pipeClient;

        private PipeClient ConnectPipe() {
            while (true) {
                try {
                    pipeClient?.Dispose();
                    pipeClient = new PipeClient(_pipeName);
                    pipeClient.Connect();
                    break; // connection success
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    pipeClient?.Dispose();
                    Thread.Sleep(1000); // sleep reconnection
                }
            }

            return pipeClient;
        }

        private void ConnectPipeServer() {
            var client = ConnectPipe();

            _transfering?.Dispose();
            _transfering = new Transfering(client.SendStream, client.ReceiveStream, BufferSize);

            var isReconnected = false;

            _transfering.ConnectionStabilised = OnConnectionStabilised + new Action(() => {
                if (isReconnected) {
                    _symbolTable = new SymbolTable(_serverQueryInterfaceProxy.GetIndexedSymbols());
                } else {
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

        public T CreateProxy<T>() {
            return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            ClassRegistrator.Register(_evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
            SpecialMessageEvt?.Invoke(sm, call);
        }

        protected virtual void OnConnectionStabilised() {
            ConnectionStabilised?.Invoke();
        }

        protected virtual void OnConnectionError(Exception ex) {
            ConnectionError?.Invoke(ex);
        }
    }

    public class PipeClient : IDisposable {
        private NamedPipeServerStream ReceivePipe;
        private NamedPipeClientStream SendPipe;
        public PipeTransferStream ReceiveStream => new PipeTransferStream(ReceivePipe, Dispose);
        public PipeTransferStream SendStream => new PipeTransferStream(SendPipe, Dispose);

        private const string ServerPref = "server-";
        private const string ClientPref = "client-";

        private object _locker = new object();

        public PipeClient(string pipeName) {
            ReceivePipe = new NamedPipeServerStream(ClientPref + pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            SendPipe = new NamedPipeClientStream(".", ServerPref + pipeName, PipeDirection.Out);
        }

        public bool Connect() {
            lock (_locker) {
                SendPipe.Connect();
                var sync = new Synchronizer(true);
                ReceivePipe.BeginWaitForConnection(ar => {
                    try {
                        ReceivePipe.EndWaitForConnection(ar);
                        sync.Unlock();
                    } catch (Exception e) {
                        Console.WriteLine(e);
                    }
                }, null);

                if (!sync.Wait(7000)) {
                    SendPipe.Close();
                    ReceivePipe.Close();
                    throw new Exception("ReceivePipe timeout");
                }

                //ReceivePipe.WaitForConnection(); does not work very well
            }

            return true;
        }

        public void Dispose() {
            ReceiveStream?.Dispose();
            SendStream?.Dispose();
        }
    }
}
