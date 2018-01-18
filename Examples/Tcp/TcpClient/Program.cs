using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ogam3.Lsp;
using Ogam3.Network.Tcp;

namespace TcpClient {
    class Program {
        static void Main(string[] args) {
            var cli = new OTcpClient("localhost", 1010);

            while (true) {
                Console.Write("CLI > ");
                var seq = Reader.Read(Console.ReadLine());
                var result = cli.Call(seq.Car() as Cons);
                Console.WriteLine($"RES > {result ?? "null"}");
            }
        }
    }
}
