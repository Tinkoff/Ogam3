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

            Evaluator.DefaultEnviroment.Define("get-context-tcp-client", new Func<dynamic>(() => GetContextTcpClient()));

            listerThread = new Thread(ListenerHandler);
            listerThread.IsBackground = true;
            listerThread.Start(_listener);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            Definer.Define(Evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        private void ListenerHandler(object o) {
            var listener = (TcpListener) o;
            while (true) {
                var client = listener.AcceptTcpClient();
                //var Thread = new Thread(ClientConnection);
                //Thread.IsBackground = true;
                //Thread.Start(client);
                ClientConnection(client);
            }
        }

        private static string ContextTcpClient = "context-tcp-client";

        public static TcpClient GetContextTcpClient() {
            return Thread.GetData(Thread.GetNamedDataSlot(ContextTcpClient)) as TcpClient;
        }

        public static IPEndPoint GetContextTcpEndPoint() {
            return (IPEndPoint)GetContextTcpClient()?.Client?.RemoteEndPoint;
        }

        private static void SetContextTcpClient(TcpClient client) {
            Thread.SetData(Thread.GetNamedDataSlot(ContextTcpClient), client);
        }

        private void ClientConnection(object o) {
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
                    Console.WriteLine(e.Message);
                    return BinFormater.Write(new SpecialMessage(e)).ToArray();
                }
            });
        }
    }
}
