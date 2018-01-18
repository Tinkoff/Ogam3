using System;
using System.IO;
using System.Net.Sockets;

namespace Ogam3.Network.Tcp {
    class NetStream : Stream {
        private TcpClient neTcpClient;
        public NetStream(TcpClient client) {
            neTcpClient = client;
        }
        public override void Flush() {
            neTcpClient.GetStream().Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return neTcpClient.GetStream().Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            neTcpClient.GetStream().Write(buffer, offset, count);
        }
        public override bool CanRead {
            //get { return neTcpClient.GetStream().CanRead && neTcpClient.GetStream().DataAvailable; }
            get { return true; }
        }
        public override bool CanSeek {
            get { return neTcpClient.GetStream().CanSeek; }
        }
        public override bool CanWrite {
            get { return neTcpClient.GetStream().CanWrite; }
        }
        public override long Length {
            get { return neTcpClient.GetStream().Length; }
        }
        public override long Position {
            get { return neTcpClient.GetStream().Position; }
            set { neTcpClient.GetStream().Position = value; }
        }
    }
}
