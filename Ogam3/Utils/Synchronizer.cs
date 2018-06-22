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
using System.Threading;

namespace Ogam3.Utils {
    public class Synchronizer : IDisposable{
        private readonly object _locker;
        private bool _isLocked;
        private readonly object _lockerProp;

        private bool IsLockedSafe {
            get {
                lock (_lockerProp) {
                    return _isLocked;
                }
            }
            set {
                lock (_lockerProp) {
                    _isLocked = value;
                }
            }
        }

        public Synchronizer(bool isLocked = false) {
            _locker = new object();
            _lockerProp = new object();
            IsLockedSafe = isLocked;
        }

        public void Lock() {
            IsLockedSafe = true;
        }

        public void Unlock() {
            IsLockedSafe = false;
            Pulse();
        }

        public void Pulse() {
            lock (_locker) {
                Monitor.PulseAll(_locker);
            }
        }

        public bool Wait() {
            if (IsLockedSafe) {
                lock (_locker) {
                    if (IsLockedSafe) {
                        return Monitor.Wait(_locker);
                    }
                }
            }

            return true;
        }

        public bool Wait(int millisecondsTimeout) {
            if (IsLockedSafe) {
                lock (_locker) {
                    if (IsLockedSafe) {
                        return Monitor.Wait(_locker, millisecondsTimeout);
                    }
                }
            }

            return true;
        }

        public bool Wait(TimeSpan timeout) {
            if (IsLockedSafe) {
                lock (_locker) {
                    if (IsLockedSafe) {
                        return Monitor.Wait(_locker, timeout);
                    }
                }
            }

            return true;
        }

        public void Dispose() {
            Unlock();
        }
    }
}