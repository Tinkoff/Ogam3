using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonInterface;
using Ogam3.Lsp;
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
    }
}
