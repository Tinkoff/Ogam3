using System;
using System.Threading;

namespace Ogam3.Utils {
    public class Synchronizer : IDisposable{
        private object _locker;
        private bool _isLocked;

        public Synchronizer(bool isLocked = false) {
            _locker = new object();
            _isLocked = isLocked;
        }

        public void Lock() {
            _isLocked = true;
        }

        public void Unlock() {
            _isLocked = false;
            Pulse();
        }

        public void Pulse() {
            lock (_locker) {
                Monitor.PulseAll(_locker);
            }
        }

        public bool Wait() {
            if (_isLocked) {
                lock (_locker) {
                    return Monitor.Wait(_locker);
                }
            }

            return true;
        }

        public bool Wait(int millisecondsTimeout) {
            if (_isLocked) {
                lock (_locker) {
                    return Monitor.Wait(_locker, millisecondsTimeout);
                }
            }

            return true;
        }

        public bool Wait(TimeSpan timeout) {
            if (_isLocked) {
                lock (_locker) {
                    return Monitor.Wait(_locker, timeout);
                }
            }

            return true;
        }

        public void Dispose() {
            Unlock();
        }
    }
}