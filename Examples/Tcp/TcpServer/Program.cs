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
            //srv.Evaluator.DefaultEnviroment
            Definer.Define(srv.Evaluator.DefaultEnviroment, new MyClass());
            //var dd = srv.Evaluator.EvlString("(qqq 1 2)");
            Console.WriteLine("OK");
            Console.ReadLine();
        }
    }

    [EnviromentAttribute ("test-env")]
    interface IInterface {
        int Test(int a, int b);
    }
    class MyClass : IInterface {
        public int Test(int a, int b) {
            return a + b;
        }
    }
}
