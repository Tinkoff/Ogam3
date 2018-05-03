/*
 * Copyright © 2018 Tinkoff Bank
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Linq;
using CommonInterface;
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