using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;

namespace Ogam3.Network.Pipe {
    public class PipeTransferStream : Stream {
        private PipeStream _pipeStream { get; }

        public PipeTransferStream(PipeStream pipe) {
            _pipeStream = pipe;
        }
        public override void Flush() {
            _pipeStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return _pipeStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            _pipeStream.Write(buffer, offset, count);
        }
        public override bool CanRead {
            get { return true; }
        }
        public override bool CanSeek {
            get { return _pipeStream.CanSeek; }
        }
        public override bool CanWrite {
            get { return _pipeStream.CanWrite; }
        }
        public override long Length {
            get { return _pipeStream.Length; }
        }
        public override long Position {
            get { return _pipeStream.Position; }
            set { _pipeStream.Position = value; }
        }
    }
}
