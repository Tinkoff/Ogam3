using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ogam3.Network.Pipe {
    public class OPipeHelper {
        private const string ServerPref = "server-";
        private const string ClientPref = "client-";

        public static string GenServerName(string pipeName) {
            return ServerPref + pipeName;
        }

        public static string GenClientName(string pipeName, int clientPid) {
            return ClientPref + pipeName + clientPid;
        }

        public static void SendClientPid(Stream stream, int pid) {
            var pidBuf = BitConverter.GetBytes(pid);
            stream.Write(pidBuf, 0, pidBuf.Length);
        }

        public static int ReadClientPid(Stream stream) {
            var pidBuf = new byte[4];
            var rCnt = stream.Read(pidBuf, 0, pidBuf.Length);

            if (rCnt != pidBuf.Length) {
                throw new Exception("Client protocol incorrect");
            }

            return BitConverter.ToInt32(pidBuf, 0);
        }
    }
}
