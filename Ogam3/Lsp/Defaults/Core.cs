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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ogam3.Lsp.Defaults {
    public class Core : EnviromentFrame {
        public Core() {
            DefineBool();
            DefineMath();
            DefineIO();
            DefineSequ();
            DefineTools();
            DefineClrTools();
        }

        void DefineBool() {
            Define("#f", false);
            Define("#t", true);
            Define("#nil", null);

            Define("=", new Func<double, double, dynamic>((a, b) => a == b));
            Define("eq?", new Func<dynamic, dynamic, dynamic>((a, b) => a == b));
            Define(">", new Func<dynamic, dynamic, dynamic>((a, b) => a > b));
            Define("<", new Func<dynamic, dynamic, dynamic>((a, b) => a < b));

            Define("not", new Func<dynamic, dynamic>((a) => !Evaluator.GetSBool(a)));

            Define("zero?", new Func<dynamic, dynamic>((a) => a == 0));

            Define("and", new Func<Params, dynamic>((par) => {
                var result = true;

                foreach (var param in par) {
                    result = result && Evaluator.GetSBool(param);

                    if (!result) {
                        return result;
                    }
                }

                return result;
            }));

            Define("or", new Func<Params, dynamic>((par) => {
                var result = false;
                foreach (var param in par) {
                    result = result || Evaluator.GetSBool(param);

                    if (result) {
                        return result;
                    }
                }

                return result;
            }));
        }

        void DefineMath() {
            Define("+", new Func<Params, dynamic>((par) => par.Aggregate((acc, p) => acc + p)));
            Define("-", new Func<Params, dynamic>((par) => par.Aggregate((acc, p) => acc - p)));
            Define("*", new Func<Params, dynamic>((par) => par.Aggregate((acc, p) => acc * p)));
            Define("/", new Func<Params, dynamic>((par) => par.Aggregate((acc, p) => acc / p)));
        }

        void DefineSequ() {
            Define("cons", new Func<dynamic, dynamic, dynamic>((a, b) => new Cons(a, b)));
            Define("car", new Func<Cons, dynamic>((a) => a.Car()));
            Define("cdr", new Func<Cons, dynamic>((a) => a.Cdr()));

            Define("set-car!", new Func<Cons, dynamic, dynamic>((cons, val) => cons.SetCar(val)));
            Define("set-cdr!", new Func<Cons, dynamic, dynamic>((cons, val) => cons.SetCdr(val)));

            Define("vector", new Func<Params, dynamic>((par) => par.ToArray()));
        }

        void DefineIO() {
            Define("display", new Func<Params, dynamic>((par) => {
                Console.Write(par[0]);
                return null;
            }));

            Define("newline", new Func<Params, dynamic>((par) => {
                Console.WriteLine("");
                return null;
            }));

            Define("read", new Func<string, dynamic>((str) => {
                if (string.IsNullOrWhiteSpace(str)) return null;

                return Reader.Read(str).Car();
            }));
        }

        void DefineTools() {
            Define("hold-process", new Func<dynamic>(() => {
                var processName = Process.GetCurrentProcess().ProcessName;
                var defColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Green;

                Console.WriteLine("The {0} is ready", processName);
                Console.WriteLine("Press <Enter> to terminate {0}", processName);

                Console.ForegroundColor = defColor;

                Console.ReadLine();
                return null;
            }));

            Define("exit", new Func<dynamic>(() => {
                Environment.Exit(0);
                return null;
            }));

            Define("begin-invoke", new Func<MulticastDelegate, Params,dynamic>((mcd, param) => {
                Task.Factory.StartNew(() => { 
                    var parameters = mcd.Method.GetParameters();
                    var cArg = new List<object>();
                    for (var i = 0; i < parameters.Length; i++) {
                        var pi = parameters[i];
                        if (typeof(Params) == pi.ParameterType) {
                            var par = new Params();
                            while (i < param.Count) {
                                par.Add(param[i++]);
                            }
                            cArg.Add(par);
                        }
                        else {
                            if (i < param.Count) {
                                cArg.Add(param[i]);
                            }
                        }
                    }


                    if (parameters.Length == cArg.Count) {
                        return mcd.DynamicInvoke(cArg.ToArray());
                    }
                    else {
                        throw new Exception($"Arity mismatch {mcd}, expected {parameters.Length}, given {cArg.Count} arguments");
                    }
                });

                return true;
            }));
        }

        void DefineClrTools() {
            //var ap = @"Z:\\GIT\\TCS\\Bellatrix\\ComonClasses\\Scanning\\bin\\Debug\\Scanning.dll";

            //$"(load-assembly \"{ap}\")".O3Eval();
            //"(define scanner (new 'Scanning.Scanner))".O3Eval();
            //"((get-member scanner 'SetDuplexMode) #t)((get-member scanner 'Scan) \"scans\")((get-member scanner 'Scan) \"scans\")".O3Eval();
            //"(define obj (new 'TcpClient.ClientLogigImplementation))".O3Eval();
            //"(set-member! obj 'Some 3)".O3Eval();
            //var tt = "((get-member obj 'Power) (get-member obj 'Some))".O3Eval();

            Define("type", new Func<object, Type>((name) => {
                if (name == null) {
                    throw new Exception("Undefined name of type.");
                }

                var fullyQualifiedName = "";
                if (name is Symbol) {
                    fullyQualifiedName = ((Symbol) name).Name;
                } else if (name is string) {
                    fullyQualifiedName = (string) name;
                }
                else {
                    new Exception("Expected Symbol or string");
                }

                if (string.IsNullOrWhiteSpace(fullyQualifiedName)) {
                    throw new Exception("Undefined name of type.");
                }

                var type = Utils.Reflect.TryFindType(fullyQualifiedName);
                if (type == null) {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                        type = asm.GetType(fullyQualifiedName);
                        if (type != null) {
                            break;
                        }
                    }

                    if (type == null) {
                        throw new Exception($"The '{fullyQualifiedName}' class not found.");
                    }
                }

                return type;
            }));

            Define("create-instance", new Func<Type, Params, dynamic>((type, args) => {
                if (type == null) {
                    throw new Exception($"Undefined type.");
                }

                //var ctors = type.GetConstructor(args.Select(a => a.GetType() as Type).ToArray());
                return Activator.CreateInstance(type, args.ToArray());
            }));



            Define("new", new Func<Params, dynamic>((par) => {
                if (!par.Any()) {
                    throw new Exception("Arity");
                }

                var args = par.Skip(1).ToArray();

                var fullyQualifiedName = "";
                if (par[0] is Symbol) {
                    fullyQualifiedName = (par[0] as Symbol).Name;
                } else if (par[0] is string) {
                    fullyQualifiedName = par[0] as string;
                } else throw new Exception("Expected Symbol or string");

                if (string.IsNullOrWhiteSpace(fullyQualifiedName)) {
                    throw new Exception("FullyQualifiedName is empty");
                }

                Type type = Type.GetType(fullyQualifiedName);
                if (type != null)
                    return Activator.CreateInstance(type, args);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    type = asm.GetType(fullyQualifiedName);
                    if (type != null)
                        return Activator.CreateInstance(type, args);
                }

                throw new Exception("Class not found");
            }));

            Define("get-member", new Func<object, object, dynamic>((inst, name) => {
                if (inst == null) return null;

                var memberName = "";
                if (name is Symbol) {
                    memberName = (name as Symbol).Name;
                } else if (name is string) {
                    memberName = name as string;
                } else throw new Exception("Expected Symbol or string");

                if (string.IsNullOrWhiteSpace(memberName)) {
                    throw new Exception("MemberName is empty");
                }

                return Utils.Reflect.GetPropValue(inst, memberName);
            }));

            Define("set-member!", new Func<object, object, object, dynamic>((inst, name, value) => {
                if (inst == null) return null;

                var memberName = "";
                if (name is Symbol) {
                    memberName = (name as Symbol).Name;
                } else if (name is string) {
                    memberName = name as string;
                } else throw new Exception("Expected Symbol or string");

                if (string.IsNullOrWhiteSpace(memberName)) {
                    throw new Exception("MemberName is empty");
                }

                return Utils.Reflect.SetPropValue(inst, memberName, value);
            }));

            Define("get-static-member", new Func<Type, object, dynamic>((type, name) => {
                if (type == null)
                    return null;

                var memberName = "";
                if (name is Symbol) {
                    memberName = (name as Symbol).Name;
                } else if (name is string) {
                    memberName = name as string;
                } else
                    throw new Exception("Expected Symbol or string");

                if (string.IsNullOrWhiteSpace(memberName)) {
                    throw new Exception("MemberName is empty");
                }

                return Utils.Reflect.GetStaticPropValue(type, memberName);
            }));

            Define("set-static-member!", new Func<Type, object, object, dynamic>((type, name, value) => {
                if (type == null)
                    return null;

                var memberName = "";
                if (name is Symbol) {
                    memberName = (name as Symbol).Name;
                } else if (name is string) {
                    memberName = name as string;
                } else
                    throw new Exception("Expected Symbol or string");

                if (string.IsNullOrWhiteSpace(memberName)) {
                    throw new Exception("MemberName is empty");
                }

                return Utils.Reflect.SetStaticPropValue(type, memberName, value);
            }));

            Define("load-assembly", new Func<string, bool>((path) => {

                if (string.IsNullOrWhiteSpace(path)) {
                    throw new Exception("Path is empty");
                }

                if (!File.Exists(path)) {
                    throw new Exception("Assembly not found");
                }

                Assembly.LoadFrom(path);

                return true;
            }));
        }
    }
}
