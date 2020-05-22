using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonInterface;
using Ogam3.Network.Pipe;
using Ogam3.Network.Tcp;
using Ogam3.Network.TCP;

namespace PipeServer {
    class Program {
        static void Main(string[] args) {
            var srv = new OPipeServer("test-pipe");

            var impl = new ServerLogicImplementation();
            // Register server implementation
            srv.RegisterImplementation(impl);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"The {Process.GetCurrentProcess().ProcessName} start");
            Console.WriteLine("Write message nad press Enter key to send notifications");
            Console.ResetColor();

            Console.ReadLine();
        }
    }


    public class ServerLogicImplementation : IServerSide {
        public int IntSumm(int a, int b) {
            return a + b;
        }

        // This method show client call from server
        public int IntSummOfPower(int a, int b) {
            var pc = OPipeServer.ContexReClient.CreateProxy<IClientSide>();
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
            var pc = OTcpServer.ContexReClient.CreateProxy<IClientSide>();
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

            if (d < 0)
                return null;

            if (d > 0) {
                return new Roots() { X1 = (-b + Math.Sqrt(d)) / (2 * a), X2 = (-b + Math.Sqrt(d)) / (2 * a) };
            }

            return new Roots() { X1 = -b / (2 * a) };
        }
    }
}
