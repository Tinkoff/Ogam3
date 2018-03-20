using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;

namespace Ogam3.Lsp {
    public class VirtualMashine {

        public static object Eval(Operation x, EnviromentFrame e) {
            return VM3(x, e);
        }
        
        public static object Eval(Operation x) {
            return VM3(x, new Core());
        }

        static object VM3(Operation operation, EnviromentFrame e) {
            object a = null;
            var x = operation;
            var size = 0;

            var trueStack = new TrueStack(100);

            while (true) {
                switch (x.Cmd) {
                    case Operation.Comand.Halt:
                        return a;
                    case Operation.Comand.Refer: {
                        a = e.Get(x.Var);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Constant: {
                        a = x.Value;
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Close: {
                        a = new Closure(x.Branch2, e, x.Vars);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Test: {
                        x = GetSBool(a) == false ? x.Branch2 : x.Branch1;
                        break;
                    }
                    case Operation.Comand.Assign: {
                        e.Set(x.Var, a);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Extend: {
                        e.Define(x.Var, a);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Conti: {
                        var var = "v".O3Symbol();
                        a = new Closure(Operation.Nuate(new TrueStack(trueStack), var), new EnviromentFrame(), new [] {var});
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Nuate: {
                        trueStack = new TrueStack((TrueStack)x.Value);
                        a = e.Get(x.Var);
                        x = Operation.Return();
                        break;
                    }
                    case Operation.Comand.Frame: {
                        trueStack.Push(size);
                        trueStack.Push(e);
                        trueStack.Push(x.Branch2);

                        x = x.Branch1;
                        
                        size = 0;
                        break;
                    }
                    case Operation.Comand.Argument: {
                        trueStack.Push(a);
                        x = x.Branch1;
                        size++;
                        break;
                    }
                    case Operation.Comand.Apply: {
                        if (a is MulticastDelegate) {
                            var func = a as MulticastDelegate;
                            var parameters = func.Method.GetParameters();

                            var argCnt = 0;
                            var cArg = new List<object>();
                            foreach (var pi in parameters) {
                                if (typeof(Params) == pi.ParameterType) {
                                    var par = new Params();
                                    while (argCnt++ < size) {
                                        par.Add(trueStack.Pop());
                                    }
                                    cArg.Add(par);
                                }
                                else {
                                    if (argCnt++ < size) {
                                        cArg.Add(trueStack.Pop());
                                    }
                                }
                            }

                            if (parameters.Length != cArg.Count) {
                                throw new Exception($"Arity mismatch {func}, expected {parameters.Length}, given {cArg.Count} arguments");
                            }

                            a = func.DynamicInvoke(cArg.ToArray());
                            x = Operation.Return();
                            break;
                        }

                        if (a is Closure) {
                            var func = a as Closure;
                            x = func.Body;
                            e = new EnviromentFrame(func.En);

                            if (size != func.Argument.Length) {
                                throw new Exception($"Arity mismatch, expected {func.Argument.Length}, given {size} arguments");
                            }

                            foreach (var arg in func.Argument) {
                                e.Define(arg, trueStack.Pop());
                            }

                            size = 0;

                            break;
                        }

                        throw new Exception($"{a} is not a callable");
                    }
                    case Operation.Comand.Return: {
                        x = (Operation)trueStack.Pop();
                        e = (EnviromentFrame)trueStack.Pop();
                        size = (int)trueStack.Pop();
                        break;
                    }
                }
            }
        }

        class TrueStack : Stack<object> {
            public TrueStack(int cap) : base(cap){}
            public TrueStack() { }

            public TrueStack(TrueStack collection) : base(collection){}
        }

        public static bool GetSBool(object o) {
            if (o == null)
                return false;
            if (o is bool)
                return (bool) o;
            else
                return true;
        }

        struct CallFrame {
            public Operation x;
            public EnviromentFrame e;
            public List<object> r;
        }

        class Closure {
            public Symbol[] Argument;
            public Operation Body;
            public EnviromentFrame En;
            public int Arity;

            public Closure(Operation body, EnviromentFrame en, Symbol[] arguments) {
                Argument = arguments;
                En = en;
                Arity = arguments.Length;
                Body = body;
            }

            public EnviromentFrame Extend(List<object> r) {
                var argCnt = Arity;
                var callEnv = new EnviromentFrame(En);

                foreach (var arg in Argument) {
                    callEnv.Define(arg, r[--argCnt]);
                }

                return callEnv;
            }
        }
    }
}
