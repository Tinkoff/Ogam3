using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonInterface;
using Ogam3;
using Ogam3.Lsp;
using Ogam3.Network.Tcp;

namespace TcpClient {
    class Program {
        static void Main(string[] args) {
            var cli = new OTcpClient("localhost", 1010);

            cli.SpecialMessageEvt += (message, o) => {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($">> {o}");
                Console.WriteLine($"<< {message}");
                Console.ResetColor();
            };

            var pc = cli.CreateInterfase<IExampleInterface>();

            Console.WriteLine($"pc.IntSumm(11, 33) = {pc.IntSumm(11, 33)}");
            Console.WriteLine($"pc.DoubleSumm(1.1, 3.3) = {pc.DoubleSumm(1.1, 3.3)}");

            pc.WriteMessage("Hello server!");

            pc.NotImplemented();

            while (true) {
                Console.Write("CLI > ");
                var seq = Reader.Read(Console.ReadLine());
                var result = cli.Call(seq.Car());
                Console.WriteLine($"RES > {result ?? "null"}");
            }
        }
    }
}