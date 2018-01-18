using System;
using System.IO;
using System.Threading;

namespace Ogam3.TxRx {
    class MemoryChannel : Stream {
        private object locker = new object();
        private MemoryStream innerStream;
        private long readPosition;
        private long writePosition;
        private const long MaxCapacity = 1024 * 1024 * 10;
        private Thread ClearThr;
        private SemaphoreSlim _writeSim;
        private SemaphoreSlim _readSim;

        public MemoryChannel() {
            innerStream = new MemoryStream();
            _writeSim = new SemaphoreSlim(1,1);
            _readSim = new SemaphoreSlim(1,1);
        }

        ~MemoryChannel() {
            ClearThr.Abort();
        }

        public override bool CanRead {
            get { return true; }
            //get { return readPosition <= innerStream.Position; }
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override bool CanWrite {
            get { return true; }
        }

        public override void Flush() {
            lock (locker) {
                innerStream.Flush();
            }
        }

        public override long Length {
            get {
                lock (locker) {
                    return innerStream.Length;
                }
            }
        }

        public override long Position {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            while (writePosition - readPosition <= 0) {
                _readSim.Wait();
            }

            lock (locker) {
                innerStream.Position = readPosition;
                int red = innerStream.Read(buffer, offset, count);
                readPosition = innerStream.Position;

                if (readPosition == writePosition) {
                    innerStream = new MemoryStream();
                    readPosition = 0;
                    writePosition = 0;
                    if (_writeSim.CurrentCount <= 0) {
                        Console.WriteLine("WRITE UNLOCK");
                        _writeSim.Release();
                    }
                }

                

                return red;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            while (writePosition >= MaxCapacity) {
                Console.WriteLine("WRITE LOCK");
                _writeSim.Wait();
            }

            lock (locker) {
                innerStream.Position = writePosition;
                innerStream.Write(buffer, offset, count);
                writePosition = innerStream.Position;

                if (_readSim.CurrentCount == 0) {
                    _readSim.Release();
                }
            }
        }
    }
}
