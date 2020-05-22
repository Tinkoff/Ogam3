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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return tcpClient.GetStream().ReadAsync(buffer, offset, count, cancellationToken);
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
