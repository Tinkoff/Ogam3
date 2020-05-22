using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ogam3.Utils {
    public class SingleThreadedSynchronizationContext : SynchronizationContext {
        private delegate void WokrDlg();
        private readonly Queue<WokrDlg> _workQueue = new Queue<WokrDlg>();
        private readonly object _syncHandle = new object();

        public SingleThreadedSynchronizationContext() {
            SetSynchronizationContext(this);
        }

        public void StartEventLoop() {
            SetSynchronizationContext(this);
            while (true) {
                WokrDlg wrk = null;
                lock (_syncHandle) {
                    if (_workQueue.Count > 0) {
                        wrk = _workQueue.Dequeue();
                    } else {
                        Monitor.Wait(_syncHandle, 7777); // wait work
                        continue;
                    }
                }

                wrk();
            }
        }

         public override void Post(SendOrPostCallback d, object state) {
            void Handle() {
                try {
                    d(state);
                } catch (Exception error) {
                    // TODO handle internally, but don't propagate it up the stack
                }
            }

            lock(_syncHandle) {
                _workQueue.Enqueue(Handle);
                Monitor.Pulse(_syncHandle);
            }
        }

        public override void Send(SendOrPostCallback d, object state) {
            d(state);
        }
    }
}
