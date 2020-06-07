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

using Ogam3.Lsp;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ogam3.Actors {
    public class Async : Async<object> {
        protected Async() : base() {}

        protected Async(Action<Async> handler) : base(handler) {}

        public static Async Success() {
            return new Async() { Result = null };
        }

        public static Async Wait(Action<Async> handler) {
            return new Async(handler);
        }

        public static new Async Default() {
            return new Async();
        }

        static Type _asyncType = typeof(Async);
        static Type _asyncGType = typeof(Async<>);
        public static Type UnwrapType(Type t) {
            return IsAsyncVType(t) ?
                typeof(void)
                : IsAsyncGType(t) ?
                t.GetGenericArguments().First()
                : t;
        }

        public static bool IsAsyncVType(Type t) {
            return t == _asyncType;
        }

        public static bool IsAsyncGType(Type t) {
            return t.IsGenericType && t.GetGenericTypeDefinition() == _asyncGType;
        }

        public static new Async Unbox(Async<object> async) {
            var asyncT = Default();
            async.Handl((res) => {
                if (res.Status == Statuses.Fault) {
                    asyncT.SpecialMessage = res.SpecialMessage;
                    asyncT.Result = null;
                } else {
                    asyncT.Result = null;
                }
            });

            return asyncT;
        }
    }

    public class Async<T> {
        public enum Statuses {
            Default,
            Wait,
            Success,
            Hanled,
            Fault
        }
        public Statuses Status { get; private set; }

        private T _result;
        public T Result {
            get => _result;
            set {
                Status = Status == Statuses.Fault ? Statuses.Fault : Statuses.Success;
                _result = value;
                OnHandle();
            } }
        private SpecialMessage _specialMessage;
        public SpecialMessage SpecialMessage {
            get => _specialMessage;
            set {
                Status = Statuses.Fault;
                _specialMessage = value;
            } }

        private Action<Async<T>> _handl;

        public Async<T> Handl(Action<Async<T>> handler) {
            _handl = handler;
            if (Status == Statuses.Success || Status == Statuses.Fault) {
                OnHandle();
            } else {
                Status = Statuses.Wait;
            }

            return this;
        }


        private void OnHandle() {
            _handl?.Invoke(this);
            Status = Statuses.Hanled;
        }

        protected Async(T value) {
            _result = value;
            Status = Statuses.Success;
        }

        protected Async(Action<Async<T>> handler) {
            _handl = handler;
            Status = Statuses.Wait;
        }

        protected Async() { }

        public static Async<T> Success(T value) {
            return new Async<T>(value);
        }

        public static Async<T> Wait(Action<Async<T>> handler) {
            return new Async<T>(handler);
        }

        public static Async<T> Default() {
            return new Async<T>();
        }

        public static Async<T> Unbox(Async<object> async) {
            var asyncT = Default();
            async.Handl((res) => {
                if (res.Status == Async<object>.Statuses.Fault) {
                    asyncT.SpecialMessage = res.SpecialMessage;
                    asyncT.Result = default;
                } else {
                    if (res.Result == null) {
                        asyncT.Result = default;
                    } else {
                        if (BinFormater.IsPrimitive(typeof(T)) || IsNullablePrimitive(typeof(T))) {
                            asyncT.Result = (T)res.Result;
                        } else {
                            asyncT.Result = (T)Serialization.OSerializer.Deserialize((Cons)res.Result, typeof(T));
                        }
                    }
                }
            });

            return asyncT;
        }

        static bool IsNullablePrimitive(Type t) {
            return IsNullable(t) && BinFormater.IsPrimitive(Nullable.GetUnderlyingType(t));
        }

        static bool IsNullable(Type t) {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
