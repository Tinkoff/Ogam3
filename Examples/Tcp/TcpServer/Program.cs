using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ogam3.Lsp;
using Ogam3.Network.Tcp;

namespace TcpServer {
    class Program {
        static void Main(string[] args) {
            var srv = new OTcpServer(1010);
            srv.Evaluator.DefaultEnviroment.Define(new Symbol("get-context-tcp-client"), new Func<dynamic>(() => {
                var client = Thread.GetData(Thread.GetNamedDataSlot("context-tcp-client"));

                return client != null;
            }));

            Console.WriteLine("OK");
            Console.ReadLine();
        }
    }
}
