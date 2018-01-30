using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ogam3.Lsp;

namespace Ogam3.Serialization {
    public class OSerializer {
        private static readonly Dictionary<Type, Func<object, Cons>> Serializers =
            new Dictionary<Type, Func<object, Cons>>();

        private static readonly Dictionary<Type, Func<Cons, object>> Deserializers =
            new Dictionary<Type, Func<Cons, object>>();

        #region Serialize

        public static Cons Serialize(object data) {
            return new Cons(new Symbol("quote"), new Cons (Serialize(data, data.GetType())));
        }

        public static Cons Serialize(object data, Type t) {
            if (data == null)
                return new Cons();
            if (Serializers.ContainsKey(t))
                return Serializers[t](data);
            lock (Serializers) {
                if (!Serializers.ContainsKey(t))
                    Serializers.Add(t, GenerateSerializer(t));
            }
            return Serializers[t](data);
        }

        public static Func<object, Cons> GenerateSerializer(Type t) {
            if (t.GetInterfaces().Any(ie => ie == typeof(ICollection))) {
                var internalType = t.GetInterface("ICollection`1").GetGenericArguments()[0];
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
            if (t.IsEnum) {
                return (obj) => new Cons(Convert.ToInt32(obj));
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                var internalType = t.GetGenericArguments()[0];
                return (obj) => {
                    if (obj == null)
                        return new Cons();
                    return BinFormater.IsPrimitive(internalType) ? new Cons(obj) : new Cons(Serialize(obj, internalType));
                };
            }
            return GeneratePropsAndFieldsSerializer(t);
        }

        public static Func<object, Cons> GeneratePropsAndFieldsSerializer(Type t) {
            var targetUnit = new CodeCompileUnit();

            var samples = GetCodeNameSpace(t);
            var targetClass = GetCodeTypeDeclaration();

            samples.Types.Add(targetClass);
            targetUnit.Namespaces.Add(samples);

            var methodName = $"Serialize{t.GetHashCode()}";

            CodeMemberMethod serializeMethod = new CodeMemberMethod {
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                Name = methodName,
                ReturnType = new CodeTypeReference(typeof(Cons))
            };
            serializeMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), "obj"));
            serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(t, "objCasted",
                new CodeCastExpression(t, new CodeArgumentReferenceExpression("obj"))));
            serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), "result",
                new CodeObjectCreateExpression(typeof(Cons))));
            serializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Cons), "current",
                new CodeVariableReferenceExpression("result")));

            var serializeExpr =
                new CodeMethodReferenceExpression(new CodeTypeReferenceExpression("OSerializer"),
                    "Serialize");

            foreach (var mb in t.GetMembers(BindingFlags.Instance | BindingFlags.Public)) {
                if (mb is FieldInfo) {
                    var f = (FieldInfo) mb;

                    if (f.IsLiteral)
                        continue;
                    SerializeMember(f.FieldType, f.Name, serializeMethod, serializeExpr);
                }
                else if (mb is PropertyInfo) {
                    var p = (PropertyInfo) mb;
                    SerializeMember(p.PropertyType, p.Name, serializeMethod, serializeExpr);
                }
            }

            CodeMethodReturnStatement returnStatement = new CodeMethodReturnStatement {
                Expression = new CodeVariableReferenceExpression("result")
            };
            serializeMethod.Statements.Add(returnStatement);
            targetClass.Members.Add(serializeMethod);

            var type = CompileUnit(t, targetUnit, "ser").CompiledAssembly.GetType("Serialization.DSerializer");
            var method = type.GetMethod(methodName);
            return obj => (Cons) method?.Invoke(null, new[] {obj});
        }

        private static void SerializeMember(Type type, string name, CodeMemberMethod serializeMethod,
            CodeMethodReferenceExpression serializeExpr) {
            var fieldExpr = new CodeFieldReferenceExpression(new CodeArgumentReferenceExpression("objCasted"), name);
            var fieldTypeExpr = new CodeTypeOfExpression(type);
            var serializeFieldExpr = new CodeMethodInvokeExpression(serializeExpr,
                new CodeExpression[] {fieldExpr, fieldTypeExpr});

            var toSymbolExpr = new CodeObjectCreateExpression(typeof(Symbol));
            toSymbolExpr.Parameters.Add(new CodePrimitiveExpression(name));

            if (type.GetInterfaces().Any(ie => ie == typeof(ICollection))) {
                serializeMethod.Statements.Add(
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(fieldExpr, CodeBinaryOperatorType.IdentityEquality,
                            new CodePrimitiveExpression(null)),
                        new CodeStatement[] {
                            new CodeAssignStatement(new CodeVariableReferenceExpression("current"),
                                new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("current"),
                                    "Add",
                                    new[] {
                                        new CodeObjectCreateExpression(typeof(Cons),
                                            toSymbolExpr,
                                            new CodePrimitiveExpression(null)
                                        )
                                    })),
                        }, new CodeStatement[] {
                            new CodeAssignStatement(new CodeVariableReferenceExpression("current"),
                                new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("current"),
                                    "Add",
                                    new[] {
                                        new CodeObjectCreateExpression(typeof(Cons),
                                            toSymbolExpr,
                                            new CodeObjectCreateExpression(typeof(Cons), serializeFieldExpr)
                                        )
                                    }))
                        }));
            }
            else if (BinFormater.IsPrimitive(type)) {
                serializeMethod.Statements.Add(
                    new CodeAssignStatement(new CodeVariableReferenceExpression("current"),
                        new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("current"), "Add",
                            new CodeExpression[] {
                                new CodeObjectCreateExpression(typeof(Cons),
                                    toSymbolExpr,
                                    fieldExpr)
                            })));
            }
            else {
                serializeMethod.Statements.Add(
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(fieldExpr, CodeBinaryOperatorType.IdentityEquality,
                            new CodePrimitiveExpression(null)), new[] {
                            new CodeAssignStatement(new CodeVariableReferenceExpression("current"),
                                new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("current"),
                                    "Add",
                                    new CodeExpression[] {
                                        new CodeObjectCreateExpression(typeof(Cons),
                                            toSymbolExpr,
                                            new CodePrimitiveExpression(null))
                                    }))
                        }, new[] {
                            new CodeAssignStatement(new CodeVariableReferenceExpression("current"),
                                new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("current"),
                                    "Add",
                                    new CodeExpression[] {
                                        new CodeObjectCreateExpression(typeof(Cons),
                                            toSymbolExpr,
                                            serializeFieldExpr)
                                    }))
                        }));
            }
        }

        #endregion

        #region Deserialize

        public static object Deserialize(Cons data, Type t) {
            if (data == null)
                return null;

            if (BinFormater.IsPrimitive(t))
                throw new ArgumentException(
                    "This method should not be used with base types (Primitive, Decimal, String, DateTime)");

            if (Deserializers.ContainsKey(t))
                return Deserializers[t](data);
            lock (Deserializers) {
                if (!Deserializers.ContainsKey(t))
                    Deserializers.Add(t, GenerateDeserializer(t));
            }
            return Deserializers[t](data);
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

        private static CodeExpression MConverter(Type memberType, CodeExpression arg) {
            if (memberType == typeof(MemoryStream)) {
                return arg;
            }

            return new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Convert)), $"To{memberType.Name}", arg);
        }

        private static int _varCount;
        private static CodeExpression MSafeCasting(Type targetType, CodeExpression exp, List<CodeStatement> statement) {

            CodeExpression typeDefaultValue = null;
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null) { // if value type create constructor for default value
                typeDefaultValue = new CodeObjectCreateExpression(targetType);
            }
            else { // else should be null
                typeDefaultValue = new CodePrimitiveExpression(null);
            }

            var testNullValue = new CodeBinaryOperatorExpression(exp, CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(null));

            var varName = $"tmpVar_{_varCount++}";
            statement.Add(new CodeVariableDeclarationStatement(targetType, varName));
            var varRef = new CodeVariableReferenceExpression(varName);
            statement.Add(new CodeConditionStatement(testNullValue, new[] {new CodeAssignStatement(varRef, typeDefaultValue)}, new[] {new CodeAssignStatement(varRef, new CodeCastExpression(targetType, exp))}));
            //var conditionalStatement = new CodeConditionStatement(testNullValue, new[] {new CodeMethodReturnStatement(typeDefaultValue)}, new[] {new CodeMethodReturnStatement(new CodeCastExpression(targetType, exp))});

            return varRef;
        }


        private static Func<Cons, object> GenerateDeserializer(Type t) {
            var targetUnit = new CodeCompileUnit();

            CodeNamespace samples = GetCodeNameSpace(t);
            var targetClass = GetCodeTypeDeclaration();

            var fullClassName = $"{samples.Name}.{targetClass.Name}";

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
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(object), "p", MCar(new CodeArgumentReferenceExpression("pair"))));
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("p"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));

                if (BinFormater.IsPrimitive(internalType))
                    deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType, "elem",
                        new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Convert)),
                            $"To{internalType.Name}",
                            new CodeArgumentReferenceExpression("p"))));
                else
                    deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType, "elem",
                        new CodeCastExpression(internalType,
                            new CodeMethodInvokeExpression(
                                new CodeTypeReferenceExpression(typeof(OSerializer)),
                                "Deserialize",
                                new CodeCastExpression(typeof(Cons), new CodeArgumentReferenceExpression("p")),
                                new CodeTypeOfExpression(internalType)))));
                if (t.GetInterfaces().Any(ie => ie == typeof(IDictionary)))
                    deserializeMethod.Statements.Add(
                        new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("result"), "Add",
                            new CodeExpression[] {
                                new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("elem"), "Key"),
                                new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("elem"), "Value"),
                            }));
                else
                    deserializeMethod.Statements.Add(
                        new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("result"), "Add",
                            new CodeExpression[] {
                                new CodeVariableReferenceExpression("elem"),
                            }));
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(
                        new CodeMethodInvokeExpression(new CodeArgumentReferenceExpression("pair"), "MoveNext"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(null)), new[] {
                        new CodeGotoStatement("outOfLoop")
                    }));
                deserializeMethod.Statements.Add(new CodeGotoStatement("insideLoop"));
                deserializeMethod.Statements.Add(new CodeLabeledStatement("outOfLoop"));
            }
            else if (t.IsEnum) {
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(object), "p",
                    MCar(new CodeArgumentReferenceExpression("pair"))));
                deserializeMethod.Statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("result"),
                    new CodeCastExpression(t, new CodeVariableReferenceExpression("p"))));
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                var internalType = t.GetInterface("ICollection`1").GetGenericArguments()[0];
                if (BinFormater.IsPrimitive(internalType))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("result"),
                        new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Convert)),
                            $"To{internalType.Name}",
                            MCar(new CodeArgumentReferenceExpression("pair")))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("result"),
                        new CodeCastExpression(internalType, new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression(typeof(OSerializer)),
                            "Deserialize",
                            MCar(new CodeArgumentReferenceExpression("pair")),
                            new CodeTypeOfExpression(internalType)))));
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) {
                var internalType1 = t.GetGenericArguments()[0];
                var internalType2 = t.GetGenericArguments()[1];
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType1, "key"));
                deserializeMethod.Statements.Add(new CodeVariableDeclarationStatement(internalType2, "value"));

                if (BinFormater.IsPrimitive(internalType1))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("key"),
                        new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Convert)),
                            $"To{internalType1.Name}",
                            MCdr(new CodeCastExpression(typeof(Cons), MCar(new CodeArgumentReferenceExpression("pair")))))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("key"),
                        new CodeCastExpression(internalType1, new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression(typeof(OSerializer)),
                            "Deserialize", new CodeCastExpression(typeof(Cons),
                                MCdr(new CodeCastExpression(typeof(Cons),MCar(new CodeArgumentReferenceExpression("pair"))))),
                            new CodeTypeOfExpression(internalType1)))));
                deserializeMethod.Statements.Add(
                    new CodeMethodInvokeExpression(new CodeArgumentReferenceExpression("pair"), "MoveNext"));

                if (BinFormater.IsPrimitive(internalType2))
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("value"),
                        new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Convert)),
                            $"To{internalType2.Name}",
                              MCdr(
                                new CodeCastExpression(typeof(Cons),
                                    MCar(new CodeArgumentReferenceExpression("pair")))))));
                else
                    deserializeMethod.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression("value"),
                        new CodeCastExpression(internalType2, new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression(typeof(OSerializer)),
                            "Deserialize", new CodeCastExpression(typeof(Cons),
                                MCdr(new CodeCastExpression(typeof(Cons),MCar(new CodeArgumentReferenceExpression("pair"))))),
                            new CodeTypeOfExpression(internalType2)))));
                deserializeMethod.Statements.Add(
                    new CodeMethodInvokeExpression(new CodeArgumentReferenceExpression("pair"), "MoveNext"));
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
                    new CodeCastExpression(typeof(Cons),
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

            //var type = CompileUnit(t, targetUnit, "deser").CompiledAssembly.GetType(nameof(OSerializer));
            var type = CompileUnit(t, targetUnit, "deser").CompiledAssembly.GetType(fullClassName);
            var method = type.GetMethod(methodName);
            return pair => method?.Invoke(null, new[] {pair});
        }

        private static void DeserializeMember(CodeMemberMethod deserializeMethod, Type memberType, string name) {
            if (memberType.GetInterfaces().Any(ie => ie == typeof(ICollection)))
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(MCar(new CodeVariableReferenceExpression("car")), "ToString"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(name)), new CodeStatement[] {
                        new CodeVariableDeclarationStatement(typeof(Cons), "prevalue",
                            new CodeCastExpression(typeof(Cons), MCdr(new CodeArgumentReferenceExpression("car")))),
                        new CodeConditionStatement(
                            new CodeBinaryOperatorExpression(
                                new CodeVariableReferenceExpression("prevalue"),
                                CodeBinaryOperatorType.IdentityEquality,
                                new CodePrimitiveExpression(null)), new CodeStatement[] {
                                new CodeAssignStatement(new CodeVariableReferenceExpression("p"),
                                    new CodeCastExpression(typeof(Cons), MCar(new CodeVariableReferenceExpression("p")))), new CodeGotoStatement("insideLoop")
                            }),
                        new CodeAssignStatement(
                            new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("result"),
                                $"{name}"), new CodeCastExpression(memberType,
                                new CodeMethodInvokeExpression(
                                    new CodeTypeReferenceExpression(typeof(OSerializer)),
                                    "Deserialize",
                                    new CodeCastExpression(typeof(Cons), MCar(new CodeArgumentReferenceExpression("prevalue"))),
                                    new CodeTypeOfExpression(memberType)))),
                        new CodeAssignStatement(new CodeVariableReferenceExpression("p"),
                            new CodeCastExpression(typeof(Cons),
                                MCdr(new CodeVariableReferenceExpression("p")))),
                        new CodeGotoStatement("insideLoop")
                    }));
            else if (BinFormater.IsPrimitive(memberType)) {

                var codeStat = new List<CodeStatement>();
                codeStat.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("result"), $"{name}"), MSafeCasting(memberType,MConverter(memberType, MCdr(new CodeArgumentReferenceExpression("car"))), codeStat)));

                //codeStat.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("p"), new CodeCastExpression(typeof(Cons), MCdr(new CodeVariableReferenceExpression("p")))));
                codeStat.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("p"), MSafeCasting(typeof(Cons), MCdr(new CodeVariableReferenceExpression("p")), codeStat)));
                codeStat.Add(new CodeGotoStatement("insideLoop"));

                deserializeMethod.Statements.Add(
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(
                                MCar(new CodeVariableReferenceExpression("car")),
                                "ToString"), 
                            CodeBinaryOperatorType.IdentityEquality, 
                            new CodePrimitiveExpression(name)),
                        codeStat.ToArray()));
            }
            else
                deserializeMethod.Statements.Add(new CodeConditionStatement(
                    new CodeBinaryOperatorExpression(new CodeMethodInvokeExpression(
                            MCar(new CodeVariableReferenceExpression("car")),
                            "ToString"),
                        CodeBinaryOperatorType.IdentityEquality,
                        new CodePrimitiveExpression(name)), new CodeStatement[] {
                        new CodeVariableDeclarationStatement(typeof(Cons), "prevalue",
                            new CodeCastExpression(typeof(Cons),
                                MCdr(new CodeArgumentReferenceExpression("car")))),
                        new CodeConditionStatement(
                            new CodeBinaryOperatorExpression(
                                new CodeVariableReferenceExpression("prevalue"),
                                CodeBinaryOperatorType.IdentityEquality,
                                new CodePrimitiveExpression(null)), new CodeStatement[] {
                                new CodeAssignStatement(new CodeVariableReferenceExpression("p"),
                                    new CodeCastExpression(typeof(Cons),
                                        MCdr(new CodeVariableReferenceExpression("p")))),
                                new CodeGotoStatement("insideLoop")
                            }),
                        new CodeAssignStatement(
                            new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("result"),
                                $"{name}"), new CodeCastExpression(memberType,
                                new CodeMethodInvokeExpression(
                                    new CodeTypeReferenceExpression(typeof(OSerializer)),
                                    "Deserialize",
                                    new CodeCastExpression(typeof(Cons),
                                        new CodeArgumentReferenceExpression("prevalue")),
                                    new CodeTypeOfExpression(memberType)))),
                        new CodeAssignStatement(new CodeVariableReferenceExpression("p"),
                            new CodeCastExpression(typeof(Cons),
                                MCdr(new CodeVariableReferenceExpression("p")))),
                        new CodeGotoStatement("insideLoop")
                    }));
        }

        #endregion

        public static List<string> RequiredNamespaces = new List<string>() {
            "System.Collections.Generic",
            "System.Collections",
            "System.Linq",
            "Ogam3",
            "Ogam3.Serialization"
        };

        private static CodeNamespace GetCodeNameSpace(Type t) {
            var result = new CodeNamespace(nameof(Serialization));

            foreach (var nmsp in RequiredNamespaces) {
                result.Imports.Add(new CodeNamespaceImport(nmsp));
            }
            foreach (var nmsp in GetTypeNamespaces(t).Where(nmsp => !RequiredNamespaces.Contains(nmsp))) {
                result.Imports.Add(new CodeNamespaceImport(nmsp));
            }
            return result;
        }

        private static CodeTypeDeclaration GetCodeTypeDeclaration() {
            return new CodeTypeDeclaration("DSerializer") {
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