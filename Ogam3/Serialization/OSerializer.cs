using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ogam3.Lsp;

namespace Ogam3.Serialization {
    public class OSerializer {

        private static readonly ConcurrentDictionary<Type, Lazy<Func<object, Cons>>> Serializers =
            new ConcurrentDictionary<Type, Lazy<Func<object, Cons>>>();

        private static readonly ConcurrentDictionary<Type, Lazy<Func<Cons, object>>> Deserializers =
            new ConcurrentDictionary<Type, Lazy<Func<Cons, object>>>();

        private static readonly List<string> RequiredNamespaces = new List<string>() {
            "System.Collections.Generic",
            "System.Collections",
            "System.Linq",
            "Ogam3",
            "Ogam3.Serialization"
        };

        private static string GeneratedClassName => "DSerializer";
        private static string GeneratedNamespace => "Ogam3.Serialization";

        #region Serialize

        public static Cons Serialize(object obj) {
            return new Cons(new Symbol("quote"), new Cons(Serialize(obj, obj.GetType())));
        }

        public static Cons Serialize(object obj, Type typeParam) {
            return obj == null
                ? new Cons()
                : Serializers.GetOrAdd(typeParam,
                        type =>
                            new Lazy<Func<object, Cons>>(() => GenerateSerializer(type),
                                LazyThreadSafetyMode.ExecutionAndPublication))?
                    .Value(obj);
        }

        public static Func<object, Cons> GenerateSerializer(Type typeParam) {
            if (typeParam.GetInterfaces().Any(ie => ie == typeof(ICollection))) {
                var internalType = typeParam.GetInterface("ICollection`1").GetGenericArguments()[0];
                return (obj) => {
                    var result = new Cons();
                    var current = result;
                    var collection = obj as ICollection;
                    collection?.Cast<object>()
                        .Aggregate(current,
                            (current1, item) =>
                                current1.Add(BinFormater.IsPrimitive(internalType)
                                    ? item
                                    : item == null ? null : Serialize(item, internalType)));
                    return result;
                };
            }
            if (typeParam.IsEnum) {
                return (obj) => new Cons(Convert.ToInt32(obj));
            }
            if (typeParam.IsGenericType && typeParam.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                var internalType = typeParam.GetGenericArguments()[0];
                return (obj) => {
                    if (obj == null)
                        return new Cons();
                    return BinFormater.IsPrimitive(internalType)
                        ? new Cons(obj)
                        : new Cons(Serialize(obj, internalType));
                };
            }
            return GeneratePropsAndFieldsSerializer(typeParam);
        }

        public static Func<object, Cons> GeneratePropsAndFieldsSerializer(Type typeParam) {
            var targetUnit = new CodeCompileUnit();

            var sampleNamespace = GetCodeNameSpace(typeParam);
            var sampleClass = GetCodeTypeDeclaration();

            sampleNamespace.Types.Add(sampleClass);
            targetUnit.Namespaces.Add(sampleNamespace);

            var methodName = $"{nameof(Serialize)}{typeParam.GetHashCode()}";
            {
                CodeMemberMethod serializeMethod = new CodeMemberMethod {
                    Attributes = MemberAttributes.Public | MemberAttributes.Static,
                    Name = methodName,
                    ReturnType = new CodeTypeReference(typeof(Cons))
                };

                string obj = "obj";
                string objCasted = "objCasted";
                string current = "current";
                string result = "result";

                serializeMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), obj));
                serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeParam, objCasted,
                    new CodeCastExpression(typeParam, new CodeArgumentReferenceExpression(obj))));
                serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), result,
                    new CodeObjectCreateExpression(typeof(Cons))));
                serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), current,
                    new CodeVariableReferenceExpression(result)));

                var serializeExpr =
                    new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(nameof(OSerializer)),
                        nameof(Serialize));

                foreach (var mb in typeParam.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                    if (mb is FieldInfo) {
                        var f = (FieldInfo) mb;

                        if (f.IsLiteral)
                            continue;
                        SerializeMember(f.FieldType, f.Name, serializeMethod, serializeExpr, objCasted, current);
                    }
                    else if (mb is PropertyInfo) {
                        var p = (PropertyInfo) mb;
                        SerializeMember(p.PropertyType, p.Name, serializeMethod, serializeExpr, objCasted, current);
                    }
                }
                CodeMethodReturnStatement returnStatement = new CodeMethodReturnStatement {
                    Expression = new CodeVariableReferenceExpression(result)
                };
                serializeMethod.Statements.Add(returnStatement);
                sampleClass.Members.Add(serializeMethod);
            }

            var type =
                CompileUnit(typeParam, targetUnit)
                    .CompiledAssembly.GetType($"{sampleNamespace.Name}.{sampleClass.Name}");
            var method = type.GetMethod(methodName);
            return obj => (Cons) method?.Invoke(null, new[] {obj});
        }

        private static void SerializeMember(Type memberType, string memberName, CodeMemberMethod serializeMethod,
            CodeMethodReferenceExpression serializeExpr, string objCasted, string current) {
            CodeExpression memberExpr =
                new CodeFieldReferenceExpression(new CodeArgumentReferenceExpression(objCasted), memberName);
            CodeExpression memberTypeExpr = new CodeTypeOfExpression(memberType);
            var serializeMemberExpr = new CodeMethodInvokeExpression(serializeExpr, new[] {memberExpr, memberTypeExpr});

            var toSymbolExpr = new CodeObjectCreateExpression(typeof(Symbol));
            toSymbolExpr.Parameters.Add(new CodePrimitiveExpression(memberName));

            if (BinFormater.IsPrimitive(memberType)) {
                serializeMethod.Statements.Add(
                    MAddConsAndAssign(new CodeVariableReferenceExpression(current), toSymbolExpr, memberExpr));
            }
            else {
                serializeMethod.Statements.Add(
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(memberExpr, CodeBinaryOperatorType.IdentityEquality,
                            new CodePrimitiveExpression(null)),
                        new[] {
                            MAddConsAndAssign(new CodeVariableReferenceExpression(current), toSymbolExpr,
                                new CodePrimitiveExpression(null))
                        }, new[] {
                            MAddConsAndAssign(new CodeVariableReferenceExpression(current), toSymbolExpr,
                                serializeMemberExpr)
                        }));
            }
        }

        #endregion

        #region Deserialize

        public static object Deserialize(Cons pair, Type typeParam) {
            return pair == null
                ? null
                : Deserializers.GetOrAdd(typeParam,
                        type =>
                            new Lazy<Func<Cons, object>>(() => GenerateDeserializer(type),
                                LazyThreadSafetyMode.ExecutionAndPublication))?
                    .Value(pair);
        }

        private static Func<Cons, object> GenerateDeserializer(Type t) {
            var targetUnit = new CodeCompileUnit();

            CodeNamespace samples = GetCodeNameSpace(t);
            var targetClass = GetCodeTypeDeclaration();

            samples.Types.Add(targetClass);
            targetUnit.Namespaces.Add(samples);

            var methodName = $"Deserialize{t.GetHashCode()}";

            CodeMemberMethod deserializeMethod = new CodeMemberMethod {
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                Name = methodName,
                ReturnType = new CodeTypeReference(typeof(object))
            };
            deserializeMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Cons), "pair"));

            deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(t, "result",
                new CodeObjectCreateExpression(t)));

            if (t.GetInterfaces().Any(ie => ie == typeof(ICollection))) {
                var internalType = t.GetInterface("ICollection`1").GetGenericArguments()[0];

                deserializeMethod.Statements.Add(new CodeLabeledStatement("insideLoop"));
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("pair"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(object), "p",
                    MCar(new CodeArgumentReferenceExpression("pair"))));
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("p"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));

                if (BinFormater.IsPrimitive(internalType))
                    deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType, "elem",
                        MAsOperatorExpression(internalType, new CodeArgumentReferenceExpression("p"))));
                else
                    deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType, "elem",
                        MAsOperatorExpression(internalType,
                            MDeserializeExpression(internalType, new CodeArgumentReferenceExpression("p")))));
                if (t.GetInterfaces().Any(ie => ie == typeof(IDictionary)))
                    deserializeMethod.Statements.Add(
                        MAddExpression(new CodeVariableReferenceExpression("result"),
                            new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("elem"), "Key")
                            , new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("elem"), "Value")));
                else
                    deserializeMethod.Statements.Add(
                        MAddExpression(new CodeVariableReferenceExpression("result"),
                            new CodeVariableReferenceExpression("elem")));
                deserializeMethod.Statements.Add(MConsNext(new CodeArgumentReferenceExpression("pair")));
                deserializeMethod.Statements.Add(new CodeGotoStatement("insideLoop"));
                deserializeMethod.Statements.Add(new CodeLabeledStatement("outOfLoop"));
            }
            else if (t.IsEnum) {
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(object), "p",
                    MCar(new CodeArgumentReferenceExpression("pair"))));
                deserializeMethod.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("result"),
                    MAsOperatorExpression(t, new CodeVariableReferenceExpression("p"))));
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                var internalType = t.GetGenericArguments()[0];
                if (BinFormater.IsPrimitive(internalType))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("result"),
                        MAsOperatorExpression(internalType, MCar(new CodeArgumentReferenceExpression("pair")))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("result"),
                        MAsOperatorExpression(internalType,
                            MDeserializeExpression(internalType, MCar(new CodeArgumentReferenceExpression("pair"))))));
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) {
                var internalType1 = t.GetGenericArguments()[0];
                var internalType2 = t.GetGenericArguments()[1];
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType1, "key"));
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType2, "value"));

                if (BinFormater.IsPrimitive(internalType1))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("key"),
                        MAsOperatorExpression(internalType1,
                            MCdr(MAsOperatorExpression(typeof(Cons), MCar(new CodeArgumentReferenceExpression("pair")))))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("key"),
                        MAsOperatorExpression(internalType1,
                            MDeserializeExpression(internalType1, MCdr(MAsOperatorExpression(typeof(Cons),
                                MCar(new CodeArgumentReferenceExpression("pair"))))))));
                deserializeMethod.Statements.Add(MConsNext(new CodeArgumentReferenceExpression("pair")));

                if (BinFormater.IsPrimitive(internalType2))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("value"),
                        MAsOperatorExpression(internalType2,
                            MCdr(MAsOperatorExpression(typeof(Cons), MCar(new CodeArgumentReferenceExpression("pair")))))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("value"),
                        MAsOperatorExpression(internalType2,
                            MDeserializeExpression(internalType2, MCdr(MAsOperatorExpression(typeof(Cons),
                                MCar(new CodeArgumentReferenceExpression("pair"))))))));
                deserializeMethod.Statements.Add(MConsNext(new CodeArgumentReferenceExpression("pair")));
                deserializeMethod.Statements.Add(new CodeAssignStatement(
                    new CodeVariableReferenceExpression("result"),
                    new CodeObjectCreateExpression(t, new CodeVariableReferenceExpression("key"),
                        new CodeVariableReferenceExpression("value"))));
            }
            else {
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), "p",
                    new CodeArgumentReferenceExpression("pair")));

                deserializeMethod.Statements.Add(new CodeLabeledStatement("insideLoop"));
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("p"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));

                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), "car",
                    MAsOperatorExpression(typeof(Cons),
                        MCar(new CodeArgumentReferenceExpression("p")))));

                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("car"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));

                foreach (var mb in t.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                    if (mb is FieldInfo) {
                        var f = (FieldInfo) mb;

                        if (f.IsLiteral)
                            continue;

                        var memberType = f.FieldType;
                        var name = f.Name;

                        DeserializeMember(deserializeMethod, memberType, name);
                    }

                    else if (mb is PropertyInfo) {
                        var p = (PropertyInfo) mb;
                        var memberType = p.PropertyType;
                        var name = p.Name;

                        DeserializeMember(deserializeMethod, memberType, name);
                    }
                }

                deserializeMethod.Statements.Add(new CodeGotoStatement("insideLoop"));
                deserializeMethod.Statements.Add(new CodeLabeledStatement("outOfLoop"));
            }

            CodeMethodReturnStatement returnStatement = new CodeMethodReturnStatement {
                Expression = new CodeVariableReferenceExpression("result")
            };

            deserializeMethod.Statements.Add(returnStatement);
            targetClass.Members.Add(deserializeMethod);

            var type = CompileUnit(t, targetUnit)
                .CompiledAssembly.GetType($"{samples.Name}.{targetClass.Name}");
            var method = type.GetMethod(methodName);
            return pair => method?.Invoke(null, new[] {pair});
        }

        private static void DeserializeMember(CodeMemberMethod deserializeMethod, Type memberType, string name) {
            var memberNameExpr = new CodeMethodInvokeExpression(MCar(new CodeVariableReferenceExpression("car")),
                "ToString");
            if (BinFormater.IsPrimitive(memberType)) {
                deserializeMethod.Statements.Add(
                    MIfEqualToMemberName(memberNameExpr, name, new CodeStatement[] {
                        new CodeAssignStatement(
                            new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("result"),
                                name),
                            MAsOperatorExpression(memberType, MCdr(new CodeArgumentReferenceExpression("car")))),
                        MConsNext(new CodeVariableReferenceExpression("p")),
                        new CodeGotoStatement("insideLoop")
                    }));
            }
            else
                deserializeMethod.Statements.Add(
                    MIfEqualToMemberName(memberNameExpr, name, new CodeStatement[] {
                        new CodeVariableDeclarationStatement(typeof(Cons), "prevalue",
                            MAsOperatorExpression(typeof(Cons), MCdr(new CodeArgumentReferenceExpression("car")))),
                        new CodeConditionStatement(
                            new CodeBinaryOperatorExpression(
                                new CodeVariableReferenceExpression("prevalue"),
                                CodeBinaryOperatorType.IdentityInequality,
                                new CodePrimitiveExpression(null)), new CodeStatement[] {
                                new CodeAssignStatement(
                                    new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("result"), name),
                                    MAsOperatorExpression(memberType,
                                        MDeserializeExpression(memberType,
                                            new CodeArgumentReferenceExpression("prevalue"))))
                            }),
                        MConsNext(new CodeVariableReferenceExpression("p")),
                        new CodeGotoStatement("insideLoop")
                    }));
        }

        #endregion

        #region M Methods

        private static CodeStatement MAddConsAndAssign(CodeVariableReferenceExpression variable, CodeExpression carParam,
            CodeExpression cdrParam) {
            return new CodeAssignStatement(variable,
                new CodeMethodInvokeExpression(variable,
                    "Add",
                    new[] {
                        new CodeObjectCreateExpression(typeof(Cons),
                            carParam,
                            cdrParam)
                    }));
        }

        private static CodeExpression MAddExpression(CodeVariableReferenceExpression var, params CodeExpression[] exprs) {
            return new CodeMethodInvokeExpression(var, "Add", exprs);
        }

        private static CodeExpression MDeserializeExpression(Type type, CodeExpression expr) {
            return new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression(typeof(OSerializer)),
                "Deserialize",
                new CodeCastExpression(typeof(Cons), expr),
                new CodeTypeOfExpression(type));
        }

        private static CodeAssignStatement MConsNext(CodeExpression var) {
            return new CodeAssignStatement(
                var, MAsOperatorExpression(typeof(Cons), MCdr(var)));
        }

        private static CodeConditionStatement MIfEqualToMemberName(CodeExpression expr, string memberName,
            CodeStatement[] ifTrue) {
            return new CodeConditionStatement(
                new CodeBinaryOperatorExpression(expr,
                    CodeBinaryOperatorType.IdentityEquality,
                    new CodePrimitiveExpression(memberName)), ifTrue);
        }

        private static CodeExpression MInvoke(CodeExpression objectRef, string methodName, params CodeExpression[] args) {
            return new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(objectRef, methodName), args);
        }

        private static CodeExpression MCar(CodeExpression objectRef) {
            return MInvoke(objectRef, "Car");
        }

        private static CodeExpression MCdr(CodeExpression objectRef) {
            return MInvoke(objectRef, "Cdr");
        }

        public static T MAsOperator<T>(object obj) {
            if (typeof(T).IsEnum)
                return (T) obj;
            return obj is T ? (T) obj : default(T);
        }

        private static CodeExpression MAsOperatorExpression(Type type, CodeExpression expr) {
            return new CodeMethodInvokeExpression(
                new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(OSerializer)),
                    "MAsOperator",
                    new CodeTypeReference[] {
                        new CodeTypeReference(type)
                    }),
                expr);
        }

        #endregion

        private static CodeNamespace GetCodeNameSpace(Type t) {
            var result = new CodeNamespace(GeneratedNamespace);

            foreach (var nmsp in RequiredNamespaces) {
                result.Imports.Add(new CodeNamespaceImport(nmsp));
            }
            foreach (var nmsp in GetTypeNamespaces(t).Where(nmsp => !RequiredNamespaces.Contains(nmsp))) {
                result.Imports.Add(new CodeNamespaceImport(nmsp));
            }
            return result;
        }

        private static CodeTypeDeclaration GetCodeTypeDeclaration() {
            return new CodeTypeDeclaration(GeneratedClassName) {
                IsClass = true,
                IsPartial = true,
                TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed
            };
        }

        private static ICollection<string> GetTypeNamespaces(Type t) {
            var res = new List<string> {t.Namespace};

            if (t.IsGenericType) {
                Array.ForEach<Type>(t.GetGenericArguments(), tt => res.AddRange(GetTypeNamespaces(tt)));
            }

            foreach (var mb in t.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                if (mb is FieldInfo) {
                    var f = (FieldInfo) mb;
                    if (BinFormater.IsPrimitive(f.FieldType))
                        continue;
                    res.AddRange(GetTypeNamespaces(f.FieldType));
                }
                else if (mb is PropertyInfo) {
                    var p = (PropertyInfo) mb;
                    if (BinFormater.IsPrimitive(p.PropertyType))
                        continue;
                    res.AddRange(GetTypeNamespaces(p.PropertyType));
                }
            }
            return res.Distinct().ToList();
        }

        private static CompilerResults CompileUnit(Type t, CodeCompileUnit targetUnit, string sourceFileName = null) {
            var provider = CodeDomProvider.CreateProvider("CSharp");

            if (!string.IsNullOrEmpty(sourceFileName)) {
                var options = new CodeGeneratorOptions();
                using (StreamWriter sourceWriter = new StreamWriter($"{sourceFileName}.cs")) {
                    provider.GenerateCodeFromCompileUnit(
                        targetUnit, sourceWriter, options);
                }
            }

            var DOMref =
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(obj => !obj.IsDynamic)
                    .Select(obj => obj.Location)
                    .ToList();

            var currentAssembly = Assembly.GetExecutingAssembly();
            DOMref.Add(currentAssembly.Location);
            var cp = new CompilerParameters(DOMref.ToArray()) {
                IncludeDebugInformation = false,
                GenerateInMemory = true
            };

            var cr = provider.CompileAssemblyFromDom(cp, new[] {targetUnit});

            if (cr.Errors.Count <= 0) return cr;

            var e = new Exception("Error in OSerializer dynamic compile module. See details in data property...");
            foreach (CompilerError ce in cr.Errors) {
                e.Data[ce.ErrorNumber] = $"{t.Assembly}|{t.Name}  {ce}";
            }

            throw e;
        }
    }
}