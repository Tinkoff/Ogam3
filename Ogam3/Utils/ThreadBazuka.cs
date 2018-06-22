using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ogam3.Utils {
    public class ThreadBazuka : IDisposable{
	    public LinkedList<Charge> ChargeHolder = new LinkedList<Charge>();

        public ThreadBazuka() {
        }

        private int _cnt;
	    public void Shot(Action action) {
	        lock (ChargeHolder) {
	                retry:
	                if (ChargeHolder.Any()) {
	                    var ch = ChargeHolder.First.Value;
	                    ChargeHolder.RemoveFirst();

	                    if (!ch.IsLive || ch.Work != null) {
	                        ch = null;
	                        goto retry;
	                    }

	                    ch.Shot(action);

	                    return;
	                }
            }



            new Charge(
	            (current) => {
	                lock (ChargeHolder) {
	                    ChargeHolder.Remove(current);
	                }
	            },
	            (current) => {
	                lock (ChargeHolder) {
	                    ChargeHolder.AddFirst(current);
	                }
	            }, action);


            if (_cnt++ > 1000000) {
                _cnt = 0;
                lock (ChargeHolder) {
                    foreach (var charge2 in ChargeHolder.ToArray()) {
                        if (!charge2.IsLive) {
                            ChargeHolder.Remove(charge2);
                        }
                    }
                }
            }
        }

        public void Dispose() {
        }
    }

	public class Charge {
		public Synchronizer Sync;
	    private Action _work;
	    private readonly object _locker = new object();
        public Action Work {
	        get {
	            lock (_locker) {
	                return _work;
	            }
            }
	        set {
	            lock (_locker) {
	                _work = value;
	            }
            }
	    }
        public bool IsLive;

	    public Charge(Action<Charge> removeFromPull, Action<Charge> addPull, Action work) {
			Sync = new Synchronizer(true);
			IsLive = true;
	        Work = work;
            lock (_locker) {
	            new Thread(() => {
	                while (true) {
                        Work?.Invoke();
                        Sync.Lock();
                        Work = null;
                        addPull(this);

                        if (!Sync.Wait(TimeSpan.FromMinutes(7))) {
                            IsLive = false;
                            Work?.Invoke();
                            removeFromPull(this);
                            Sync.Dispose();
                            return;
	                    }
	                }
	            }) {IsBackground = true}.Start();
	        }
	    }

		public void Shot(Action work) {
		    lock (_locker) {
                Work = work;
		        Sync.Unlock();
            }
		}
	}
}
