using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonInterface;
using Ogam3.Lsp;
using Ogam3.Network.Pipe;

namespace PipeClient {
    class Program {
        static void Main(string[] args) {
            var cli = new OPipeClient("test-pipe");

            cli.RegisterImplementation(new ClientLogigImplementation());

            cli.SpecialMessageEvt += (message, o) => {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($">> {o}");
                Console.WriteLine($"<< {message}");
                Console.ResetColor();
            };

            // Create proxy
            var pc = cli.CreateProxy<IServerSide>();

            Console.WriteLine($"pc.IntSumm(11, 33) = {pc.IntSumm(11, 33)}");
            Console.WriteLine($"pc.DoubleSumm(1.1, 3.3) = {pc.DoubleSumm(1.1, 3.3)}");
            Console.WriteLine($"pc.IntSummOfPower(11, 33) = {pc.IntSummOfPower(11, 33)}");

            Console.WriteLine($"pc.QuadraticEquation(1,2,3) = {QuadraticString(pc.QuadraticEquation(1, 2, 3))}");
            Console.WriteLine($"pc.QuadraticEquation(2,4,-7) = {QuadraticString(pc.QuadraticEquation(2, 4, -7))}");
            Console.WriteLine($"pc.QuadraticEquation(1,6,9) = {QuadraticString(pc.QuadraticEquation(1, 6, 9))}");

            pc.WriteMessage("Hello server!");

            pc.NotImplemented();

            var dto = new ExampleDTO() {
                DateTimeValue = DateTime.Now,
                DoubleValue = 11.33,
                IntegerValue = 1133,
                StringValue = "String message",
                IntList = new int[100].ToList(),
                StreamValue = new MemoryStream(new byte[5000])
            };

            var echoDto = pc.TestSerializer(dto);

            Console.WriteLine($"DTO {echoDto?.DateTimeValue}");

            Console.Write("Press Enter to continue...");
            Console.ReadLine();
        }

        private static string QuadraticString(Roots? r) {
            if (!r.HasValue) {
                return "No roots";
            }

            if (r.Value.X2.HasValue && r.Value.X1.HasValue) {
                return $"X1 = {r.Value.X1}, X2 = {r.Value.X2}";
            }

            if (r.Value.X1.HasValue) {
                return $"X1 = {r.Value.X1}";
            }

            return "error";
        }
    }

    public class ClientLogigImplementation : IClientSide {
        public int Power(int x) {
            return x * x;
        }

        public void Notify(string msg) {
            Console.WriteLine($"SERVER-NOTIFY> {msg}");
        }
    }
}
