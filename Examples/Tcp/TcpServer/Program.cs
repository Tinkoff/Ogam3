using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ogam3.Network.Tcp;

namespace TcpServer {
    class Program {
        static void Main(string[] args) {
            var srv = new OTcpServer(1010);
            Console.WriteLine("OK");
            Console.ReadLine();
        }
    }
}
