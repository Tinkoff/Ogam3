using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.Network.Tcp;
using Ogam3.Network.TCP;
using Ogam3.TxRx;

namespace Ogam3.Network.Pipe {
    public class OPipeServer {
        public uint BufferSize = 65535;
        public Evaluator Evaluator;
        private Thread listerThread;

        public static Action<string> Log = Console.WriteLine;

        private readonly QueryInterface _queryInterface;

        private string _pipeName;

        public OPipeServer(string pipeName, Evaluator evaluator = null) {
            _pipeName = pipeName;
            Evaluator = evaluator ?? new Evaluator();

            _queryInterface = new QueryInterface();
            _queryInterface.UpsertIndexedSymbols(new[] { "quote", "lambda", "if", "begin", "define", "set!", "call/cc", });
            _queryInterface.UpsertIndexedSymbols(Evaluator.DefaultEnviroment.Variables.Keys.ToArray());
            RegisterImplementation(_queryInterface);

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;
            listerThread.Start();
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            var symbols = ClassRegistrator.Register(Evaluator.DefaultEnviroment, instanceOfImplementation);
            _queryInterface.UpsertIndexedSymbols(symbols);
        }

        private void ListenerHandler(object o) {
            while (true) {
                var server = new PipeServer(_pipeName);
                server.WaitConnection();
                ClientConnection(server);
            }
        }

        private static void SetContextObj(string id, object obj) {
            Thread.SetData(Thread.GetNamedDataSlot(id), obj);
        }

        public static object GetContextObj(string id) {
            return Thread.GetData(Thread.GetNamedDataSlot(id));
        }

        private static string ReClientId = "context-re-client";
        public static OTcpServer.ReClient ContexReClient => (OTcpServer.ReClient)GetContextObj(ReClientId);

        private void ClientConnection(PipeServer pserver) {
            //var client = (TcpClient)o;
            //var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
            //Log?.Invoke($"(client-connected \"{endpoint.Address}:{endpoint.Port}\")");

            //var ns = new NetStream(client);

            var server = new Transfering(pserver.SendStream, pserver.ReceiveStream, BufferSize);

            server.StartReceiver(data => {
                //SetContextObj(ContextTcpClientId, client); // TODO single set
                SetContextObj(ReClientId, new OTcpServer.ReClient(server, Evaluator, _queryInterface)); // TODO single set

                return DataHandler(Evaluator, data, _queryInterface.GetSymbolTable());
            });
        }

        public static byte[] DataHandler(Evaluator evl, byte[] data, SymbolTable symbolTable) {
            try {
                var receive = BinFormater.Read(new MemoryStream(data), symbolTable);

                var transactLog = new StringBuilder();
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
                var sb = new StringBuilder();
                while (ex != null) {
                    sb.AppendLine(ex.Message);
                    ex = ex.InnerException;
                }
                Log?.Invoke(sb.ToString());
                return BinFormater.Write(new SpecialMessage(sb.ToString()), symbolTable).ToArray();
            }
        }
    }

    public class PipeServer : IDisposable{
        private NamedPipeServerStream ReceivePipe;
        private NamedPipeClientStream SendPipe;
        public PipeTransferStream ReceiveStream => new PipeTransferStream(ReceivePipe, Dispose);
        public PipeTransferStream SendStream => new PipeTransferStream(SendPipe, Dispose);

        private const string ServerPref = "server-";
        private const string ClientPref = "client-";
        private string _pipeName;

        public PipeServer(string pipeName) {
            _pipeName = pipeName;
            ReceivePipe = new NamedPipeServerStream(ServerPref + pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances);
            //SendPipe = new NamedPipeClientStream(".", ClientPref + pipeName, PipeDirection.Out);
        }

        private object _locker = new object();
        public bool WaitConnection() {
            lock (_locker) {
                ReceivePipe.WaitForConnection();
                var pidBuf = new byte[4];
                var r = ReceivePipe.Read(pidBuf, 0, pidBuf.Length);
                var pid = BitConverter.ToInt32(pidBuf,0);
                SendPipe = new NamedPipeClientStream(".", ClientPref + _pipeName + pid.ToString(), PipeDirection.Out);
                SendPipe.Connect();
            }

            return true;
        }

        public void Dispose() {
            ReceiveStream?.Dispose();
            SendStream?.Dispose();
        }
    }
}
