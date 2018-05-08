using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ogam3.Utils {
    public class ThreadBazuka : IDisposable{
        public List<Charge> ChargeHolder;
        public uint Min = 1;
        public TimeSpan ChargeLiveTime;
        public TimeSpan DeadTime;
        private bool isLive;

        public ThreadBazuka() {
            ChargeHolder = new List<Charge>();
            ChargeLiveTime = TimeSpan.FromMinutes(1);
            DeadTime = TimeSpan.FromMinutes(10);

            Alive();
        }

        private void InitPull() {
            PreCreate(Min);
        }

        private void Alive() {
            if (isLive) return;
            isLive = true;

            InitPull();

            new Thread(() => {
                while (isLive) {
                    lock (ChargeHolder) {
                        var now = DateTime.Now;
                        if (ChargeHolder.Count > Min) {
                            var olds = ChargeHolder.Where(c => c.IsLive && c.Work == null && (now - c.LastShot) > ChargeLiveTime).ToArray();
                            var diff = (ChargeHolder.Count - Min) * 0.3;
                            var i = 0;
                            foreach (var charge in olds) {
                                charge.Kill();
                                ChargeHolder.Remove(charge);

                                if (i++ >= diff) {
                                    break;
                                }
                            }
                        }
                        else {
                            if (ChargeHolder.All(c => (now - c.LastShot) > DeadTime)) {
                                foreach (var charge in ChargeHolder.ToArray()) {
                                    charge.Kill();
                                    ChargeHolder.Remove(charge);
                                }
                                isLive = false;
                                return;
                            }
                        }
                    }

                    Thread.Sleep(ChargeLiveTime);
                }
            }) {IsBackground = true}.Start();
        }

        public void Shot(Action action) {
            lock (ChargeHolder) {
                var charge = ChargeHolder.FirstOrDefault(c => c.IsLive && c.Work == null);

                if (charge != null) {
                    charge.Shot(action);
                    return;
                }
            }

            var newCharge = new Charge();
            newCharge.Shot(action + (Alive));
            lock (ChargeHolder) {
                ChargeHolder.Add(newCharge);
            }
        }

        private void PreCreate(uint count) {
            var lst = new List<Charge>();
            for (var i = 0; i < count; i++) {
                lst.Add(new Charge());
            }

            lock (ChargeHolder) {
                ChargeHolder.AddRange(lst);
            }
        }

        public void Dispose() {
            isLive = false;
            lock (ChargeHolder) {
                foreach (var charge in ChargeHolder.ToArray()) {
                    charge.Kill();
                    ChargeHolder.Remove(charge);
                }
            }
        }
    }

    public class Charge {
        public Synchronizer Sync;
        public Action Work;
        public DateTime LastShot;
        public bool IsLive;

        public Charge() {
            LastShot = DateTime.Now;
            IsLive = true;
            Sync = new Synchronizer();
            Sync.Lock();

            new Thread(() => {
                while (IsLive) {
                    Work?.Invoke();
                    Sync.Lock();
                    Work = null;
                    Sync.Wait(TimeSpan.FromMinutes(1));
                }
            }) {IsBackground = true}.Start();
            
        }

        public void Kill() {
            IsLive = false;
            Sync.Unlock();
        }

        public void Shot(Action work) {
            LastShot = DateTime.Now;
            Work = work;
            Sync.Unlock();
        }
    }
}
