using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Ogam3.Lsp {
    class Core : EnviromentFrame {
        public Core() {
            DefineBool();
            DefineMath();
            DefineMath();
            DefineIO();
            DefineSequ();
            DefineTools();
        }

        void DefineBool() {
            Define("#f", false);
            Define("#t", true);

            Define("=", new Func<double, double, dynamic>((a, b) => a == b));
            Define("eq?", new Func<dynamic, dynamic, dynamic>((a, b) => a == b));
            Define(">", new Func<dynamic, dynamic, dynamic>((a, b) => a > b));
            Define("<", new Func<dynamic, dynamic, dynamic>((a, b) => a < b));

            Define("not", new Func<dynamic, dynamic>((a) => Evaluator.GetSBool(a)));

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
    }
}
