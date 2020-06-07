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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommonInterface;
using Ogam3.Actors;
using Ogam3.Lsp;
using Ogam3.Network.Tcp;
using Ogam3.Network.TCP;

namespace TcpClient {
    class Program {
        static void Main(string[] args) {
            // Create connection
            var cli = new OTcpClient("localhost", 1010);

            //var a = Async<string>.Default();
            //cli.AsyncCall(null).Handl((res) => {
            //    a.Result = res.Result as string;
            //});

            //Async<T> Unbox<T>(Async<object> async) {
            //    var asyncT = Async<T>.Default();
            //    async.Handl((res) => {
            //        asyncT.Result = (T)res.Result;
            //    });

            //    return asyncT;
            //}

            //Async<string>.Unbox(cli.AsyncCall(null));

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


            // Async call
            for (var i = 0; i < 100; i++) {
                pc.AsyncStringCall($"CALL {i}").Handl(d => {
                    Console.WriteLine(d.Result);
                });
            }

            pc.AsyncVoidCall().Handl(d => {
                Console.WriteLine(d.Status);
            });

            Console.WriteLine("WAIT");

            Console.ReadLine();

            // Server calls
            Console.WriteLine($"pc.IntSumm(11, 33) = {pc.IntSumm(11, 33)}");
            Console.WriteLine($"pc.DoubleSumm(1.1, 3.3) = {pc.DoubleSumm(1.1, 3.3)}");
            Console.WriteLine($"pc.IntSummOfPower(11, 33) = {pc.IntSummOfPower(11, 33)}");

            Console.WriteLine($"pc.QuadraticEquation(1,2,3) = {QuadraticString(pc.QuadraticEquation(1,2,3))}");
            Console.WriteLine($"pc.QuadraticEquation(2,4,-7) = {QuadraticString(pc.QuadraticEquation(2,4,-7))}");
            Console.WriteLine($"pc.QuadraticEquation(1,6,9) = {QuadraticString(pc.QuadraticEquation(1,6,9))}");

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