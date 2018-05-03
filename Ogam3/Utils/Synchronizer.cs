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