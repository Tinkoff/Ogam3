using System;
using System.IO;
using System.Net.Sockets;

namespace Ogam3.Network.Tcp {
    class NetStream : Stream {
        private TcpClient tcpClient { get; }

        public NetStream(TcpClient client) {
            tcpClient = client;
        }
        public override void Flush() {
            tcpClient.GetStream().Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return tcpClient.GetStream().Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            tcpClient.GetStream().Write(buffer, offset, count);
        }
        public override bool CanRead {
            get { return true; }
        }
        public override bool CanSeek {
            get { return tcpClient.GetStream().CanSeek; }
        }
        public override bool CanWrite {
            get { return tcpClient.GetStream().CanWrite; }
        }
        public override long Length {
            get { return tcpClient.GetStream().Length; }
        }
        public override long Position {
            get { return tcpClient.GetStream().Position; }
            set { tcpClient.GetStream().Position = value; }
        }
    }
}
