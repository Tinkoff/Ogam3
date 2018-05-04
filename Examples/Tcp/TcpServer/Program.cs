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
using CommonInterface;
using Ogam3.Network.Tcp;
using Ogam3.Utils;

namespace TcpServer {
    class Program {
        static void Main(string[] args) {
            // Set log mode
            LogTextWriter.InitLogMode();
            // Start listener
            var srv = new OTcpServer(1010);
            // Create server instance
            var impl = new ServerLogicImplementation();
            // Register server implementation
            srv.RegisterImplementation(impl);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"The {Process.GetCurrentProcess().ProcessName} start");
            Console.WriteLine("Write message nad press Enter key to send notifications");
            Console.ResetColor();
            while (true) {
                var msg = Console.ReadLine();
                foreach (var client in impl.Subscribes) {
                    client.Notify(msg);
                }
                Console.WriteLine($"{impl.Subscribes.Count} notification{(impl.Subscribes.Count > 1 ? "s" : "")}");
            }
        }
    }

    public class ServerLogicImplementation : IServerSide {
        public int IntSumm(int a, int b) {
            return a + b;
        }

        // This method show client call from server
        public int IntSummOfPower(int a, int b) {
            var pc = OTcpServer.ContexReClient.CreateInterfase<IClientSide>();
            return pc.Power(a) + pc.Power(b);
        }

        public double DoubleSumm(double a, double b) {
            return a + b;
        }

        public void WriteMessage(string text) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public void NotImplemented() {
            throw new NotImplementedException();
        }

        public ExampleDTO TestSerializer(ExampleDTO dto) {
            return dto;
        }

        public List<IClientSide> Subscribes = new List<IClientSide>();
        public void Subscribe() {
            var pc = OTcpServer.ContexReClient.CreateInterfase<IClientSide>();
            lock (Subscribes) {
                if (!Subscribes.Contains(pc)) {
                    Subscribes.Add(pc);
                    OTcpServer.ContexReClient.ConnectionError += exception => {
                        lock (Subscribes) {
                            Subscribes.Remove(pc);
                        }
                    };
                }
            }
        }

        public Roots? QuadraticEquation(double? a, double? b, double? c) {
            if (!(a.HasValue && b.HasValue && c.HasValue)) {
                return null;
            }

            var d = Math.Pow(b.Value, 2) - 4 * a.Value * c.Value;

            if (d < 0) return null;

            if (d > 0) {
                return new Roots() {X1 = (-b + Math.Sqrt(d)) / (2 * a), X2 = (-b + Math.Sqrt(d)) / (2 * a)};
            }

            return new Roots() {X1 = -b / (2 * a)};
        }
    }
}
