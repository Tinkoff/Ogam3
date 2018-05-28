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
		    Charge ch = null;

			lock (ChargeHolder) {
				retry:
				if (ChargeHolder.Any()) {
				    ch = ChargeHolder.First.Value;
					ChargeHolder.RemoveFirst();

					if (!ch.IsLive) {
						ch = null;
						goto retry;
					}

				    ch.Shot(action + (() => {
					    lock (ChargeHolder) {
						    ChargeHolder.AddFirst(ch);
					    }
				    }));

					return;
				}
		    }

		    ch = new Charge(() => {
			    lock (ChargeHolder) {
				    ChargeHolder.Remove(ch);
			    }
		    });


		    ch.Shot(action + (() => {
			    lock (ChargeHolder) {
				    ChargeHolder.AddFirst(ch);
			    }
		    }));

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
		public Action Work;
		public bool IsLive;
		public Charge(Action removeFromPull) {
			Sync = new Synchronizer();
			Sync.Lock();
			IsLive = true;

			new Thread(() => {
				while (true) {
					Work?.Invoke();
					Sync.Lock();
					Work = null;
					if (!Sync.Wait(TimeSpan.FromMinutes(7))) {
						IsLive = false;
						Work?.Invoke();
						removeFromPull();
						Sync.Dispose();
						return;
					}
				}
			}) { IsBackground = true }.Start();

		}

		public void Shot(Action work) {
			Work = work;
			Sync.Unlock();
		}
	}
}
