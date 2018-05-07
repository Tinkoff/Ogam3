using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ogam3.Network.Tcp;

namespace PerformanceNetCompare {
    class Program {
        static void Main(string[] args) {
            var soapUrl = $"http://{Environment.MachineName}:{1155}";
            var soapBaseAddress = new Uri(soapUrl);
            var soapHost = new ServiceHost(typeof(Gate), soapBaseAddress);
            soapHost.Open();

            var restUrl = $"http://{Environment.MachineName}:{1156}";
            var restBaseAddress = new Uri(restUrl);
            var restHost = new WebServiceHost(typeof(Gate), restBaseAddress);
            restHost.Open();

            OTcpServer.Log = null;
            var srv = new OTcpServer(1157);
            var impl = new Gate();
            srv.RegisterImplementation(impl);

            Thread.Sleep(3000);

            var dto = Dto.GetObject();

            var pc = new OTcpClient(Environment.MachineName, 1157).CreateProxy<IGate>();

            SoapCall(dto, soapUrl);
            RestCall(dto, restUrl);
            O3Call(dto, pc);

            var cnt = 300000;

            Console.Write($"Soap cnt = {cnt} elapsed = ");
            MultiCall(cnt, () => {
                SoapCall(dto, soapUrl);
            });

            Console.Write($"Rest cnt = {cnt} elapsed = ");
            MultiCall(cnt, () => {
                RestCall(dto, restUrl);
            });

            Console.Write($"O3 cnt = {cnt} elapsed = ");
            MultiCall(cnt, () => {
                O3Call(dto, pc);
            });
            

            Console.ReadLine();
        }

        private static void MultiCall(int cnt, Action act) {
            var sw = new Stopwatch();

            sw.Start();
            for (var i = 0; i < cnt; i++) {
                act();
            }
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
        }

        private static void O3Call(Dto dto, IGate pc) {
            pc.Echo(dto);
        }

        private static void SoapCall(Dto dto, string soapUrl) {
            var cli = new ChannelFactory<IGate>(new BasicHttpBinding(), soapUrl);
            cli.Open();
            cli.CreateChannel().Echo(dto);
        }

        private static void RestCall(Dto dto, string restUrl) {
            var json = SerializeDTO(dto);

            var request = WebRequest.Create(restUrl + "/Echo");
            request.Method = "POST";
            request.ContentLength = json.Length;
            request.ContentType = "application/json; charset=utf-8";

            using (var writer = new StreamWriter(request.GetRequestStream())) {
                writer.Write(json);
            }

            var responce = request.GetResponse();

            var res = DeserializeCheckResult<Dto>(responce.GetResponseStream());
        }


        public static string SerializeDTO(object dto) {
            var ser = new DataContractJsonSerializer(dto.GetType());
            using (var ms = new MemoryStream()) {
                ser.WriteObject(ms, dto);
                ms.Position = 0;
                using (var sr = new StreamReader(ms)) {
                    return sr.ReadToEnd();
                }
            }
        }

        public static T DeserializeCheckResult<T>(Stream jStream) {
            var ser = new DataContractJsonSerializer(typeof(T));
            return (T)ser.ReadObject(jStream);
        }

        public static MemoryStream String2Stream(string value) {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }
    }
}
