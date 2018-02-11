using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Ogam3.Network;
using Ogam3.Network.Tcp;
using Ogam3.Serialization;

namespace Ogam3.Lsp {
    static class Definer {
        public static void Define(EnviromentFrame env, object instanceOfImplementation) { // TODO is draft solution
            const BindingFlags methodFlags =
                BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;
            var type = instanceOfImplementation.GetType();
            foreach (var interfaceType in type.GetInterfaces()
                .Where(t => t.GetCustomAttributes(typeof(EnviromentAttribute), false).Any())) {
                //var envAtt = (EnviromentAttribute) interfaceType.GetCustomAttribute(typeof(EnviromentAttribute));
                var envAtt = (EnviromentAttribute) interfaceType.GetCustomAttributes(typeof(EnviromentAttribute), true).FirstOrDefault();
                foreach (var interfaceMethodInfo in interfaceType.GetMethods(methodFlags)) {
                    var implMethod = type.GetMethod(interfaceMethodInfo.Name, methodFlags);

                    if (implMethod == null) continue;

                    var funcArgs = implMethod.GetParameters().Select(p => p.ParameterType)
                        .Concat(new[] {implMethod.ReturnType}).ToArray();
                    var delegateType = Expression.GetDelegateType(funcArgs);
                    //var callableDelegate = implMethod.CreateDelegate(delegateType, instanceOfImplementation);
                    var callableDelegate = Delegate.CreateDelegate(delegateType, instanceOfImplementation, implMethod);


                    var shell = new Func<Params, object>((par) =>  { // TODO tmp solution reaplace atogenerated code
                        var finalArgLst = new List<object>();

                        for (var i = 0; i < par.Count; i++) {
                            if (BinFormater.IsPrimitive(funcArgs[i])) {
                                finalArgLst.Add(par[i]);
                            }
                            else {
                                finalArgLst.Add(OSerializer.Deserialize(par[i] as Cons, funcArgs[i]));
                            }
                        }

                        var result = callableDelegate.DynamicInvoke(finalArgLst.ToArray());

                        if (result == null) return null;

                        if (BinFormater.IsPrimitive(result.GetType())) {
                            return result;
                        }

                        return OSerializer.SerializeOnly(result);
                    });

                    Func<string, bool> isEmpty = string.IsNullOrWhiteSpace;

                    var defineName = isEmpty(envAtt.EnviromentName)
                        ? implMethod.Name
                        : $"{envAtt.EnviromentName}:{implMethod.Name}";

                    if (env.Lookup(new Symbol(defineName))) {
                        throw new Exception($"Name conflict '{defineName}' interface {interfaceType.FullName}");
                    }

                    //env.Define(defineName, callableDelegate);
                    env.Define(defineName, shell);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class EnviromentAttribute : Attribute {
        public string EnviromentName { get; }

        public EnviromentAttribute(string enviromentName) {
            EnviromentName = enviromentName;
        }
    }
}