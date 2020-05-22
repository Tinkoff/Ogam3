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

using Ogam3.TxRx;
using Ogam3.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Ogam3.Actors {
    public class OTActorEngine {
        Queue<OTContext> _incomingQueue;
        ThreadBazuka _threadBazuka;
        Synchronizer _sync;

        private readonly object _syncHandle = new object();

        public OTActorEngine() {
            _incomingQueue = new Queue<OTContext>();
            _threadBazuka = new ThreadBazuka();
            _sync = new Synchronizer();

            new Thread(() => { 
                while(true) {
                    try {
                        OTContext context = null;
                        lock (_syncHandle) {
                            if (_incomingQueue.Count > 0) {
                                context = _incomingQueue.Dequeue();
                            } else {
                                Monitor.Wait(_syncHandle, 7777); // wait work
                                continue;
                            }
                        }

                        _threadBazuka.Shot(() => {
                            context.Callback(context);
                        });

                    } catch(Exception e) {
                        Console.WriteLine(e);
                        Thread.Sleep(500);
                    }
                }
            }) { IsBackground = true }.Start();
        }

        public void EnqueueData(OTContext context) {
            lock (_syncHandle) {
                _incomingQueue.Enqueue(context);
                Monitor.Pulse(_syncHandle);
            }
        }

    }
}
