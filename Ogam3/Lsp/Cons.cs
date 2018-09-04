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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ogam3.Lsp {
    public class Cons {
        private dynamic _car;
        private dynamic _cdr;

        public object Car() {
            return _car;
        }

        public object Cdr() {
            return _cdr;
        }

        public Cons SetCar(object obj) {
            _car = obj;
            return this;
        }

        public Cons SetCdr(object obj) {
            _cdr = obj;
            return this;
        }

        public Cons() { }

        public Cons(dynamic car, dynamic cdr = null) {
            _car = car;
            _cdr = cdr;
        }

        private Cons _lastElement; // optimisation
        public Cons Add(dynamic o) {
            lock (this) {

            if (_car == null && _cdr == null) {
                _car = o;
                return this;
            }

            if (_lastElement == null) _lastElement = this;
                while (_lastElement._cdr != null) {
                    if (_lastElement._cdr is Cons) {
                        _lastElement = _lastElement._cdr as Cons;
                    }
                    else {
                        break;
                    }
                }

                var cndr = new Cons(o);
                _lastElement._cdr = cndr;
                _lastElement = cndr;

                return cndr;
            }
        }

        public static StringPack SPack(string str) {
            return new StringPack(str);
        }

        public static Cons Exp(params object[] items) {
            return List(items.Select<object,object>(i => {
                if (i is string) {
                    var s = i as string;
                    if (s.StartsWith("$$")) {
                        return s.Remove(0, 2);
                    }
                    return new Symbol(s);
                }

                if (i is StringPack) {
                    return i.ToString();
                }

                return i;
            }).ToArray());
        }

        public static Cons List(params object[] items) {
            var root = new Cons();

            foreach (var item in items) {
                root.Add(item);
            }

            return root;
        }

        public override string ToString() {
            if (_car == null && _cdr == null) {
                return "()";
            }

            var sb = new StringBuilder();

            sb.Append("(");
            sb.Append(_car != null ? O2String(_car) : "()");

            if (_cdr is Cons) {
                var cndr = _cdr as Cons;
                while (cndr != null) {
                    sb.Append(" ");
                    sb.Append(O2String(cndr._car));
                    if (cndr._cdr is Cons) {
                        cndr = cndr._cdr as Cons;
                    } else {
                        if (cndr._cdr != null) {
                            sb.Append(" . ");
                            sb.Append(O2String(cndr._cdr));
                        }
                        break;
                    }
                }
            } else {
                if (_cdr != null) {
                    sb.Append(" . ");
                    sb.Append(O2String(_cdr));
                }
            }


            sb.Append(")");
            return sb.ToString();
        }

        public static string O2String(object o) {
            if (o == null)
                return "#nil";

            var str = "";
            if (IsBaseType(o) || o is Cons || o is Symbol) {
                if (o is decimal) {
                    str = ((decimal)o).ToString(CultureInfo.InvariantCulture);
                } else if (o is float) {
                    str = ((float)o).ToString(CultureInfo.InvariantCulture);
                } else if (o is double) {
                    //str = Convert.ToDecimal((double)o).ToString(CultureInfo.InvariantCulture);
                    str = ((double)o).ToString(CultureInfo.InvariantCulture);
                } else {
                    str = o.ToString();
                }
            } else if (o is string) {
                str = (string)o;
                str = str.Replace("\\", "\\\\");
                str = str.Replace("\"", "\\\"");
                str = (string.Format("\"{0}\"", str));
            } else if (o is bool) {
                str = string.Format("{0}", ((bool)o ? "#t" : "#f"));
            } else if (o is DateTime) {
                str = string.Format("\"{0}\"", ((DateTime)o).ToString("dd.MM.yyyy HH:mm:ss.fff"));
            } else {
                str = string.Format("\"{0}\"", o.ToString());
            }


            return str;
        }

        private static bool IsBaseType(dynamic o) {
            return (o is byte)
                   || (o is sbyte)
                   || (o is char)
                   || (o is decimal)
                   || (o is double)
                   || (o is float)
                   || (o is int)
                   || (o is uint)
                   || (o is long)
                   || (o is ulong)
                   || (o is short)
                   || (o is ushort);
        }

        public IEnumerable<dynamic> GetIterator() {
            var cndr = this;

            while (cndr != null) {
                yield return cndr;
                cndr = cndr._cdr as Cons;
            }
        }

        public dynamic AsObject() {
            return _car;
        }

        public string AsString() {
            var o = AsObject();

            return o == null ? "" : Convert.ToString(o);
        }

        public int AsInt() {
            var o = AsObject();

            return o == null ? 0 : Convert.ToInt32(o);
        }

        public double AsDouble() {
            var o = AsObject();

            return o == null ? 0.0 : Convert.ToDouble(o);
        }

        public bool AsBool() {
            var o = AsObject();

            return o != null && Convert.ToBoolean(o);
        }

        public Cons MoveNext() {
            var next = _cdr as Cons;
            return next != null ? new Cons(next._car, next._cdr) : null;
        }

        public int Count() {
            return GetIterator().Count();
        }
    }

    public static class ExtendObj {
        public static object Car<T>(this T obj) {
            return (obj as Cons)?.Car();
        }

        public static object Cdr<T>(this T obj) {
            return (obj as Cons)?.Cdr();
        }

        //public static object Car<T>(this T obj) {
        //    return Caro(obj);
        //}

        //public static object Cdr<T>(this T obj) {
        //    return Cdro(obj);
        //}

        //public static object Caro(object obj) {
        //    if (obj is Cons) {
        //        return ((Cons)obj).Car();
        //    }
        //    return null;
        //}

        //public static object Cdro(object obj) {
        //    if (obj is Cons) {
        //        return ((Cons)obj).Cdr();
        //    }
        //    return null;
        //}

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source) {
            return source ?? Enumerable.Empty<T>();
        }
    }
}