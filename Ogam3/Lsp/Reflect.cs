using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ogam3.Lsp {
    public class Reflect {
        public static object GetPropValue(object obj, string propName) {
            string[] nameParts = propName.Split('.');
            foreach (var part in nameParts) {
                if (obj == null) {
                    return null;
                }

                var splt = part.Split('[');
                var key = splt.First();
                var textIndex = splt.ElementAtOrDefault(1)?.Replace("]", "");


                var info = obj.GetType().GetMember(key);
                obj = GetValue(info.First(), obj);

                if (!string.IsNullOrWhiteSpace(textIndex)) {
                    try {
                        obj = (obj as IList)[Convert.ToInt32(textIndex)];
                    } catch (Exception e) {
                        obj = null;
                    }
                }
            }
            return obj;
        }

        private static object GetValue(MemberInfo memberInfo, object forObject) {
            if (forObject == null)
                return null;

            switch (memberInfo.MemberType) {
                case MemberTypes.Field:
                    return ((FieldInfo)memberInfo).GetValue(forObject);
                case MemberTypes.Property:
                    return ((PropertyInfo)memberInfo).GetValue(forObject, null);
                case MemberTypes.Method: {
                     var mi = ((MethodInfo) memberInfo);
                    var par = mi.GetParameters().Select(p => p.ParameterType).ToList();
                    par.Add(mi.ReturnType);
                    return Delegate.CreateDelegate(System.Linq.Expressions.Expression.GetDelegateType(par.ToArray()), forObject, mi.Name);
                }
                default:
                    throw new NotImplementedException();
            }
        }

        public static bool SetPropValue(object obj, string propName, object value) {
            if (obj == null)
                return false;

            var nameParts = propName.Split('.');
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
            } else {
                try {
                    obj = GetPropValue(obj, key);
                    if (obj == null)
                        return false;

                    (obj as IList)[Convert.ToInt32(textIndex)] = value;
                } catch (Exception e) {
                    return false;
                }
            }

            return true;
        }

        private static void SetValue(MemberInfo memberInfo, object forObject, object value) {
            if (forObject == null)
                return;

            switch (memberInfo.MemberType) {
                case MemberTypes.Field:
                    ((FieldInfo)memberInfo).SetValue(forObject, Convert.ChangeType(value, ((FieldInfo) memberInfo).FieldType));
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo)memberInfo).SetValue(forObject, Convert.ChangeType(value, ((PropertyInfo) memberInfo).PropertyType), null);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
