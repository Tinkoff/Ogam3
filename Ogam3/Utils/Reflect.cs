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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ogam3.Utils {
    public class Reflect {

        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        public static Type TryFindType(string typeName) {
            Type type;
            lock (_typeCache) {
                if (!_typeCache.TryGetValue(typeName, out type)) {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                        type = assembly.GetType(typeName);
                        if (type != null) {
                            _typeCache[typeName] = type;
                            break;
                        }
                    }
                }
            }
            return type;
        }

        public static object GetStaticPropValue(Type type, string propName) {
            var nameParts = propName.Split('.');

            var first = nameParts.First();
            var splt = first.Split('[');
            var key = splt.First();
            var textIndex = splt.ElementAtOrDefault(1)?.Replace("]", "");
            var mi = type.GetMember(key).FirstOrDefault();
            var obj = GetValue(mi, type, null);

            if (!string.IsNullOrWhiteSpace(textIndex)) {
                try {
                    obj = (obj as IList)[Convert.ToInt32(textIndex)];
                }
                catch (Exception) {
                    obj = null;
                }
            }

            return GetPropValue(obj, nameParts.Skip(1).ToArray());
        }

        public static object GetPropValue(object obj, string propName) {
            var nameParts = propName.Split('.');
            return GetPropValue(obj, nameParts);
        }

        public static object GetPropValue(object obj, string[] nameParts) {
            foreach (var part in nameParts) {
                if (obj == null) {
                    return null;
                }

                var splt = part.Split('[');
                var key = splt.First();
                var textIndex = splt.ElementAtOrDefault(1)?.Replace("]", "");


                var info = obj.GetType().GetMember(key);
                obj = GetValue(info.First(), null, obj);

                if (!string.IsNullOrWhiteSpace(textIndex)) {
                    try {
                        obj = (obj as IList)[Convert.ToInt32(textIndex)];
                    }
                    catch (Exception) {
                        obj = null;
                    }
                }
            }

            return obj;
        }

        private static object GetValue(MemberInfo memberInfo, Type tagetType, object forObject) {
            switch (memberInfo.MemberType) {
                case MemberTypes.Field:
                    return ((FieldInfo) memberInfo).GetValue(forObject);
                case MemberTypes.Property:
                    return ((PropertyInfo) memberInfo).GetValue(forObject, null);
                case MemberTypes.Method: {
                    var mi = ((MethodInfo) memberInfo);
                    var par = mi.GetParameters().Select(p => p.ParameterType).ToList();
                    par.Add(mi.ReturnType);
                    var delegateType = System.Linq.Expressions.Expression.GetDelegateType(par.ToArray());
                    return forObject == null
                        ? Delegate.CreateDelegate(delegateType, tagetType, mi.Name)
                        : Delegate.CreateDelegate(delegateType, forObject, mi.Name);
                }
                default:
                    throw new NotImplementedException();
            }
        }

        public static bool SetPropValue(object obj, string propName, object value) {
            return SetPropValue(obj, propName.Split('.'), value);
        }

        public static bool SetPropValue(object obj, string[] nameParts, object value) {
            if (obj == null)
                return false;

            if (nameParts.Length != 1) {
                var sb = new StringBuilder();
                for (var i = 0; i < nameParts.Length - 1; i++) {
                    if (i > 0) {
                        sb.Append(".");
                    }

                    sb.Append(nameParts[i]);
                }

                obj = GetPropValue(obj, sb.ToString());
            }

            if (obj == null)
                return false;

            var splt = nameParts.Last().Split('[');
            var key = splt.First();
            var textIndex = splt.ElementAtOrDefault(1)?.Replace("]", "");

            if (string.IsNullOrWhiteSpace(textIndex)) {
                SetValue(obj.GetType().GetMember(key).First(), obj, value);
            }
            else {
                try {
                    obj = GetPropValue(obj, key);
                    if (obj == null)
                        return false;

                    (obj as IList)[Convert.ToInt32(textIndex)] = value;
                }
                catch (Exception) {
                    return false;
                }
            }

            return true;
        }

        public static bool SetStaticPropValue(Type type, string propName, object value) {
            // TODO
            var nameParts = propName.Split('.');

            if (nameParts.Length == 1) {
                // static set
                var first = nameParts.First();
                var splt = first.Split('[');
                var key = splt.First();
                var textIndex = splt.ElementAtOrDefault(1)?.Replace("]", "");
                var mi = type.GetMember(key).FirstOrDefault();
                if (splt.Length > 1) {
                    var firstArrayObject = GetValue(mi, type, null);

                    if (!string.IsNullOrWhiteSpace(textIndex)) {
                        (firstArrayObject as IList)[Convert.ToInt32(textIndex)] = value;
                        return true;
                    }
                }

                SetValue(mi, null, value);
                return true;
            }

            var firstObject = GetStaticPropValue(type, nameParts.FirstOrDefault()); // get object from static field
            return SetPropValue(firstObject, nameParts.Skip(1).ToArray(), value); // set value
        }

        private static bool SetValue(MemberInfo memberInfo, object forObject, object value) {
            switch (memberInfo.MemberType) {
                case MemberTypes.Field:
                    ((FieldInfo) memberInfo).SetValue(forObject,
                        Convert.ChangeType(value, ((FieldInfo) memberInfo).FieldType));
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo) memberInfo).SetValue(forObject,
                        Convert.ChangeType(value, ((PropertyInfo) memberInfo).PropertyType), null);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return true;
        }
    }
}