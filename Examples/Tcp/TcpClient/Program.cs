using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
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
            // Create connection
            var cli = new OTcpClient("localhost", 1010);
            // Register client interface implementation
            cli.RegisterImplementation(new ClientLogigImplementation());
            // Set server error handler
            cli.SpecialMessageEvt += (message, o) => {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($">> {o}");
                Console.WriteLine($"<< {message}");
                Console.ResetColor();
            };
            // Create proxy
            var pc = cli.CreateProxy<IServerSide>();
            // Server calls
            Console.WriteLine($"pc.IntSumm(11, 33) = {pc.IntSumm(11, 33)}");
            Console.WriteLine($"pc.DoubleSumm(1.1, 3.3) = {pc.DoubleSumm(1.1, 3.3)}");
            Console.WriteLine($"pc.IntSummOfPower(11, 33) = {pc.IntSummOfPower(11, 33)}");

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

            Console.WriteLine($"DTO {echoDto.DateTimeValue}");

            pc.Subscribe();

            while (true) {
                Console.Write("CLI > ");
                var seq = Reader.Read(Console.ReadLine());
                var result = cli.Call(seq.Car());
                Console.WriteLine($"RES > {result ?? "null"}");
            }
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