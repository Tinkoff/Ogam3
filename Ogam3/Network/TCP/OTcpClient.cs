using System;
using System.IO;
using System.Net.Sockets;
using Ogam3.Lsp;
using Ogam3.TxRx;

namespace Ogam3.Network.Tcp {
    class OTcpClient {
        public string Host;
        public int Port;

        public TcpClient ClientTcp;
        private Transfering Transfering;
        private static int timeout = 60000;
        public uint BufferSize = 1048576;
        public NetworkStream Stream;


        public OTcpClient(string host, int port) {
            Host = host;
            Port = port;

            ClientTcp = new TcpClient();
            //ClientTcp.ReceiveTimeout = timeout;
            //ClientTcp.SendTimeout = timeout;
            ClientTcp.Connect(Host, Port);

            //Stream = ClientTcp.GetStream();
            //NetworkStream.WriteTimeout = timeout;
            //NetworkStream.ReadTimeout = timeout;

            var ns = new NetStream(ClientTcp);

            Transfering = new Transfering(ns, ns, BufferSize);
            Transfering.StartReceiver(data => {
                Console.WriteLine($"Client receive {data.Length}Bt");
                return new byte[0];
            });
        }

        public object Call(Cons seq) {
            var res = BinFormater.Read(new MemoryStream(Transfering.Send(BinFormater.Write(seq).ToArray())));
            return res.Car();
        }
    }
}
