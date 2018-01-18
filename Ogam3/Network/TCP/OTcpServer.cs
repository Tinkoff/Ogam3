using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.TxRx;

namespace Ogam3.Network.Tcp {
    public class OTcpServer {
        private readonly TcpListener _listener;
        private Thread listerThread;
        public uint BufferSize = 1048576;
        public Evaluator Evaluator;

        public OTcpServer(int port, Evaluator evaluator = null) {
            if (evaluator == null) {
                Evaluator = new Evaluator();
            }

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;
            listerThread.Start(_listener);
        }

        private void ListenerHandler(object o) {
            var listener = (TcpListener) o;
            while (true) {
                var client = listener.AcceptTcpClient();
                var Thread = new Thread(ClientThread);
                Thread.IsBackground = true;
                Thread.Start(client);
            }
        }

        public static TcpClient GetContextTcpClient() {
            return Thread.GetData(Thread.GetNamedDataSlot("context-tcp-client")) as TcpClient;
        }

        public static IPEndPoint GetContextTcpEndPoint() {
            return (IPEndPoint)GetContextTcpClient()?.Client?.RemoteEndPoint;
        }

        private static void SetContextTcpClient(TcpClient client) {
            Thread.SetData(Thread.GetNamedDataSlot("context-tcp-client"), client);
        }

        private void ClientThread(object o) {
            var client = (TcpClient) o;
            var endpoint = (IPEndPoint) client.Client.RemoteEndPoint;
            Console.WriteLine($"(client-connected \"{endpoint.Address}:{endpoint.Port}\")");

            var ns = new NetStream(client);

            var server = new Transfering(ns, ns, BufferSize);
            server.StartReceiver(data => {
                SetContextTcpClient(client);

                var receive = BinFormater.Read(new MemoryStream(data));

                try {
                    var res = Evaluator.EvlSeq(receive);
                    if (res != null) {
                        return BinFormater.Write(res).ToArray();
                    } else {
                        return new byte[0];
                    }
                } catch (Exception e) {
                    return BinFormater.Write(e.ToString()).ToArray();
                }
            });
        }
    }

    //public class ContextTcpClient : IContextProperty {
    //    public TcpClient TcpClient { get; }

    //    public ContextTcpClient(TcpClient client) {
    //        TcpClient = client;
    //    }

    //    public bool IsNewContextOK(Context newCtx) {
    //        throw new NotImplementedException();
    //    }

    //    public void Freeze(Context newContext) {
    //        throw new NotImplementedException();
    //    }

    //    public string Name => "context-tcp-client";
    //}
}
