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

namespace Ogam3.Lsp {
    static class Definer {
        public static void Define(EnviromentFrame env, object instanceOfImplementation) { // TODO is draft solution
            const BindingFlags methodFlags =
                BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;
            var type = instanceOfImplementation.GetType();
            foreach (var interfaceType in type.GetInterfaces()
                .Where(t => t.GetCustomAttributes(typeof(EnviromentAttribute), false).Any())) {
                var envAtt = (EnviromentAttribute) interfaceType.GetCustomAttribute(typeof(EnviromentAttribute));
                foreach (var interfaceMethodInfo in interfaceType.GetMethods(methodFlags)) {
                    var implMethod = type.GetMethod(interfaceMethodInfo.Name, methodFlags);

                    if (implMethod == null) continue;

                    var funcArgs = implMethod.GetParameters().Select(p => p.ParameterType)
                        .Concat(new[] {implMethod.ReturnType}).ToArray();
                    var delegateType = Expression.GetDelegateType(funcArgs);
                    var callableDelegate = implMethod.CreateDelegate(delegateType, instanceOfImplementation);

                    Func<string, bool> isEmpty = string.IsNullOrWhiteSpace;

                    var defineName = isEmpty(envAtt.EnviromentName)
                        ? implMethod.Name
                        : $"{envAtt.EnviromentName}:{implMethod.Name}";

                    if (env.Lookup(new Symbol(defineName))) {
                        throw new Exception($"Name conflict '{defineName}' interface {interfaceType.FullName}");
                    }

                    env.Define(defineName, callableDelegate);
                }
            }
        }

        private const string NameOfClientField = "_client";

        public static string GenerateSharpCode(Type serverInterface, ISomeClient client) {
            var instantName = "RC" + client.GetType().Name + serverInterface.Name;
            var nameSpaceName = client.GetType().Name + "Caller";

            var codeRoot = CreateDomObject(serverInterface, client, nameSpaceName, instantName);

            return CreateSrc(codeRoot);
        }

        private static Dictionary<string, Type> _callerTypeStore = new Dictionary<string, Type>();

        public static object CreateTcpCaller(Type serverInterface, ISomeClient client) {
            //var code = GenerateSharpCode(serverInterface, client);

            var instantName = "RC" + client.GetType().Name + serverInterface.Name;
            var nameSpaceName = client.GetType().Name + "Caller";

            var fullName = $"{nameSpaceName}.{instantName}";

            Type callerType = null;
            lock (_callerTypeStore) { // TODO improve lock
                if (_callerTypeStore.TryGetValue(fullName, out callerType)) {
                    return Activator.CreateInstance(callerType, client);
                }

                var codeRoot = CreateDomObject(serverInterface, client, nameSpaceName, instantName);

                var compiler = CodeDomProvider.CreateProvider("CSharp");

                var DOMref =
                    AppDomain.CurrentDomain.GetAssemblies()
                        .Where(obj => !obj.IsDynamic)
                        .Select(obj => obj.Location)
                        .ToList();

                var currentAssembly = Assembly.GetExecutingAssembly();
                DOMref.Add(currentAssembly.Location);

                var compilerParams = new CompilerParameters(DOMref.ToArray());
                compilerParams.GenerateInMemory = true;
                compilerParams.GenerateExecutable = false;
                compilerParams.IncludeDebugInformation = false;

                var cr = compiler.CompileAssemblyFromDom(compilerParams, codeRoot);

                foreach (var ce in cr.Errors) {
                    throw new Exception(ce.ToString());
                }

                callerType = cr.CompiledAssembly.GetType($"{nameSpaceName}.{instantName}");

                _callerTypeStore[fullName] = callerType;
            }

            return Activator.CreateInstance(callerType, client);
        }

        static CodeCompileUnit CreateDomObject(Type serverInterface, ISomeClient client, string nameSpaceName, string instantName) {
            var codeRoot = new CodeCompileUnit();

            var codeNamespace = new CodeNamespace(nameSpaceName);
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Collections"));
            //codeNamespace.Imports.Add(new CodeNamespaceImport("System.Linq"));


            foreach (var nmsp in GetTypeNamespaces(client.GetType())) {
                codeNamespace.Imports.Add(new CodeNamespaceImport(nmsp));
            }

            foreach (var nmsp in GetTypeNamespaces(serverInterface)) {
                codeNamespace.Imports.Add(new CodeNamespaceImport(nmsp));
            }

            codeRoot.Namespaces.Add(codeNamespace);

            var targetClass = new CodeTypeDeclaration(instantName);
            targetClass.IsClass = true;
            targetClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            targetClass.BaseTypes.Add(serverInterface);

            targetClass.Members.Add(AddField(NameOfClientField, typeof(ISomeClient)));

            targetClass.Members.Add(CreateCtor());


            const BindingFlags methodFlags =
                BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            var envAtt = (EnviromentAttribute) serverInterface.GetCustomAttribute(typeof(EnviromentAttribute));

            foreach (var interfaceMethodInfo in serverInterface.GetMethods(methodFlags)) {
                var funcArgs = interfaceMethodInfo.GetParameters().ToArray();
                var retType = interfaceMethodInfo.ReturnType;

                Func<string, bool> isEmpty = string.IsNullOrWhiteSpace;
                var defineName = isEmpty(envAtt.EnviromentName)
                    ? interfaceMethodInfo.Name
                    : $"{envAtt.EnviromentName}:{interfaceMethodInfo.Name}";

                targetClass.Members.Add(CreateMethod(interfaceMethodInfo.Name, defineName, funcArgs, retType));
            }

            codeNamespace.Types.Add(targetClass);

            return codeRoot;
        }

        static CodeMemberField AddField(string name, Type type) {
            var field = new CodeMemberField();
            field.Attributes = MemberAttributes.Private;
            field.Name = name;
            field.Type = new CodeTypeReference(type);

            return field;
        }

        private static ICollection<string> GetTypeNamespaces(Type t) {
            var res = new List<string> {t.Namespace};

            if (t.IsGenericType) {
                Array.ForEach<Type>(t.GetGenericArguments(), tt => res.AddRange(GetTypeNamespaces(tt)));
            }

            foreach (var mb in t.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                if (mb is FieldInfo) {
                    var f = (FieldInfo) mb;
                    if (IsBaseType(f.FieldType))
                        continue;
                    res.AddRange(GetTypeNamespaces(f.FieldType));
                }
                else if (mb is PropertyInfo) {
                    var p = (PropertyInfo) mb;
                    if (IsBaseType(p.PropertyType))
                        continue;
                    res.AddRange(GetTypeNamespaces(p.PropertyType));
                }
            }
            return res.Distinct().ToList();
        }

        private static bool IsBaseType(Type t) {
            return t.IsPrimitive || t == typeof(string) || t == typeof(DateTime) || t == typeof(Decimal);
        }

        static CodeMemberMethod CreateMethod(string methodName, string defineName, ParameterInfo[] arguments, Type returnType) {
            var callMethod = new CodeMemberMethod();
            callMethod.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            callMethod.Name = methodName;
            callMethod.Parameters.AddRange(arguments.Select(a => new CodeParameterDeclarationExpression(a.ParameterType, a.Name)).ToArray());
            callMethod.ReturnType = new CodeTypeReference(returnType);

            var tcpClientRef = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), NameOfClientField);

            var symbolNameRef = new CodeObjectCreateExpression(typeof(Symbol), new CodePrimitiveExpression(defineName));

            var listBuilderParams = new List<CodeExpression>();
            listBuilderParams.Add(symbolNameRef);

            foreach (var arg in arguments) {

                //**************************************************
                //**  TODO AT THIS LINE SHOULD BE THE SERIALIZER  **
                //**************************************************

                listBuilderParams.Add(new CodeArgumentReferenceExpression(arg.Name));
            }

            var consLst = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Cons)), nameof(Cons.List), listBuilderParams.ToArray());

            var invoke = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(tcpClientRef, nameof(ISomeClient.Call)), consLst);

            // IS VOID METHOD
            if (returnType == typeof(void)) {
                callMethod.Statements.Add(invoke);
            }
            else {
                const string requestResultVarName = "requestResult";
                callMethod.Statements.Add(
                    new CodeVariableDeclarationStatement(typeof(object), requestResultVarName, invoke));
                var requestResultRef = new CodeVariableReferenceExpression(requestResultVarName);

                var eql = new CodeBinaryOperatorExpression(requestResultRef, CodeBinaryOperatorType.ValueEquality,
                    new CodePrimitiveExpression(null));

                //****************************************************
                //**  TODO AT THIS LINE SHOULD BE THE DESERIALIZER  **
                //****************************************************

                var castType = new CodeCastExpression(returnType, requestResultRef);

                CodeStatement returnDefaultValue = null;
                if (returnType.IsValueType && Nullable.GetUnderlyingType(returnType) == null) {
                    returnDefaultValue = new CodeMethodReturnStatement(new CodeObjectCreateExpression(returnType));
                }
                else {
                    returnDefaultValue = new CodeMethodReturnStatement(new CodePrimitiveExpression(null));
                }

                var conditionalStatement = new CodeConditionStatement(eql, new[] {returnDefaultValue},
                    new[] {new CodeMethodReturnStatement(castType)});
                callMethod.Statements.Add(conditionalStatement);
            }

            return callMethod;
        }


        static CodeConstructor CreateCtor() {
            var constructor = new CodeConstructor();
            constructor.Attributes = MemberAttributes.Public | MemberAttributes.Final;

            const string nameOfClientArg = "client";

            constructor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(ISomeClient), nameOfClientArg));

            var tcpClientReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), NameOfClientField);
            constructor.Statements.Add(new CodeAssignStatement(tcpClientReference, new CodeArgumentReferenceExpression(nameOfClientArg)));

            return constructor;
        }

        static string CreateSrc(CodeCompileUnit codeCompileUnit) {
            var provider = CodeDomProvider.CreateProvider("CSharp");
            using (var sourceWriter = new StringWriter()) {
                provider.GenerateCodeFromCompileUnit(codeCompileUnit, sourceWriter, new CodeGeneratorOptions());
                return sourceWriter.ToString();
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