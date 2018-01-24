using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ogam3.Lsp {
    public class Definer {
        public static void Define(EnviromentFrame env, object instanceOfImplementation) { // TODO is draft solution
            const BindingFlags methodFlags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;
            var type = instanceOfImplementation.GetType();
            foreach (var interfaceType in type.GetInterfaces().Where(t => t.GetCustomAttributes(typeof(EnviromentAttribute), false).Any())) {
                var envAtt = (EnviromentAttribute)interfaceType.GetCustomAttribute(typeof(EnviromentAttribute));
                foreach (var interfaceMethodInfo in interfaceType.GetMethods(methodFlags)) {
                    var implMethod = type.GetMethod(interfaceMethodInfo.Name, methodFlags);

                    if (implMethod == null) continue;
                    
                    var funcArgs = implMethod.GetParameters().Select(p => p.ParameterType).Concat(new[] { implMethod.ReturnType }).ToArray();
                    var delegateType = Expression.GetDelegateType(funcArgs);
                    var callableDelegate = implMethod.CreateDelegate(delegateType, instanceOfImplementation);

                    Func<string, bool> isEmpty = string.IsNullOrWhiteSpace;

                    var defineName = isEmpty(envAtt.EnviromentName) ? implMethod.Name : $"{envAtt.EnviromentName}:{implMethod.Name}";

                    if (env.Lookup(new Symbol(defineName))) {
                        throw new Exception($"Name conflict '{defineName}' interface {interfaceType.FullName}");
                    }

                    env.Define(defineName, callableDelegate);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class EnviromentAttribute:Attribute {
        public string EnviromentName { get; }

        public EnviromentAttribute(string enviromentName = null) {
            EnviromentName = enviromentName;
        }
    }
}
