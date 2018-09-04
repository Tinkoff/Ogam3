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
using System.Linq;
using System.Runtime.InteropServices;

namespace Ogam3.Lsp {
    public class Compiler {
        public static Operation Compile(string str) {
            return Compile(Reader.Read(str));
        }

        public static EnviromentFrame<Macro> macro = new EnviromentFrame<Macro>();

        public static Cons MExpand(Cons seq) { // TODO
            var rseq = new Cons();
            foreach (var o in seq.GetIterator()) {
                var exp = o.Car();

                if (exp is Cons) {
                    var op = exp.Car() as Symbol;
                    var arguments = exp.Cdr() as Cons;

                    if (op?.Name == "defmacro") {
                        var mName = arguments?.Car().Car() as Symbol;
                        var mArg = arguments?.Car().Cdr() as Cons;
                        var mBody = arguments?.Cdr() as Cons;
                        macro.Define(mName, new Macro(mArg, mBody));
                        continue;
                        
                    }
                    else if(macro.Lookup(op)) {
                        foreach (var o1 in (macro.Get(op).Expand(arguments)).GetIterator()) {
                            rseq.Add(o1.Car());
                        }

                        continue;
                    }
                }

                rseq.Add(exp);
            }
            return rseq;
        }

        public class Macro { // TODO
            public Symbol[] Args;
            public Cons Body;

            public Macro(Cons args, Cons body) {
                Args = args.GetIterator().Select(i => i.Car() as Symbol).ToArray();
                Body = body;
            }

            public Cons Expand(Cons values) {
                var vls = values.GetIterator().ToArray();
                var dic = new Dictionary<string, object>();

                for (int i = 0; i < Args.Length; i++) {
                    dic[Args[i].Name] = vls[i].Car();
                }

                return Replacer(Body, dic);
            }

            private Cons Replacer(Cons seq, Dictionary<string, object> vals) {
                var rSeq = new Cons();
                foreach (var o in seq.GetIterator()) {
                    var itm = o.Car();
                    if (itm is Cons) {
                        itm = Replacer(itm as Cons, vals);
                    } else if (itm is Symbol) {
                        if (vals.ContainsKey((itm as Symbol).Name)) {
                            itm = vals[(itm as Symbol).Name];
                        }
                    }

                    rSeq.Add(itm);
                }
                return rSeq;
            }
        }

        public static Operation Compile(Cons seq) {
            //return CompileBegin(MExpand(seq), Operation.Halt());
            return CompileBegin(seq, Operation.Halt());
        }

        public static Operation CompileBegin(Cons seq, Operation next) {
            var res = Operation.Nop();

            return seq.GetIterator().Reverse().Aggregate(res, (current, exp) => Compile(exp.Car(), current.Cmd == Operation.Comand.Nop ? next : current));
        }

        public static Operation Compile(object exp, Operation next) {
            if (exp is Symbol) {
                return Operation.Refer(exp as Symbol, next);
            }

            if (exp is Cons) {
                var op = exp.Car() as Symbol;
                var arguments = exp.Cdr() as Cons;

                switch (op?.Name) {
                    case "quote":
                        return Operation.Constant(arguments?.Car(), next);
                    case "lambda": {
                        var args = arguments.Car().Car() == null ? new Symbol[0] : (arguments.Car() as Cons).GetIterator().Select(i => (Symbol) i.Car()).ToArray();
                        return Operation.Close(args, CompileBegin(arguments?.Cdr() as Cons, Operation.Return()), next);
                    }
                    case "if": {
                        var thenc = Compile(arguments?.Cdr().Car(), next);
                        var elsec = Compile(arguments?.Cdr().Cdr().Car(), next);
                        return Compile(arguments?.Car(), Operation.Test(thenc, elsec));
                    }
                    case "begin": {
                        return CompileBegin(arguments, next);
                    }
                    case "define": {
                        var defi = arguments?.Car();
                        if (defi is Symbol) { // variable
                            return Compile(arguments?.Cdr().Car(), Operation.Extend((Symbol) defi, next));
                        }

                        if (defi is Cons) { // function
                            var name = (Symbol) defi.Car();
                            var args = defi.Cdr().Car() == null ? new Symbol[0] : (defi.Cdr() as Cons).GetIterator().Select(i => i.Car() as Symbol).ToArray();
                            return Operation.Close(args, CompileBegin(arguments?.Cdr() as Cons, Operation.Return()), Operation.Extend(name, next));
                        }

                        throw new Exception($"define bad syntax: {exp}");
                    }
                    case "set!":
                        return Compile(arguments?.Cdr().Car(), Operation.Assign(arguments?.Car() as Symbol, next));
                    case "call/cc": {
                        var c2 = Operation.Conti(Operation.Argument(Compile(arguments?.Car(), Operation.Apply())));
                        if (next.Cmd == Operation.Comand.Return) {
                            return c2;
                        }

                        return Operation.Frame(c2, next);
                    }
                    default: {
                        var c2 = Compile(exp.Car(), Operation.Apply());

                        if (arguments != null) {
                            foreach (Cons o in arguments.GetIterator()) {
                                c2 = Compile(o.Car(), Operation.Argument(c2));
                            }
                        }

                        if (next.Cmd == Operation.Comand.Return) {
                            return c2;
                        }

                        return Operation.Frame(c2, next);
                    }
                }
            }
            else {
                return Operation.Constant(exp, next);
            }
        }
    }

    public class Operation {
        public Comand Cmd;

        public Operation Branch1;
        public Operation Branch2;
        public Symbol Var;
        public Symbol[] Vars;
        public object Value;

        public enum Comand {
            Nop,
            Halt,
            Refer,
            Constant,
            Close,
            Test,
            Extend,
            Assign,
            Conti,
            Nuate,
            Frame,
            Argument,
            Apply,
            Return
        }

        public Operation(Comand cmd) {
            Cmd = cmd;
        }

        public static Operation Nop() {
            return new Operation(Comand.Nop);
        }

        public static Operation Halt() {
            return new Operation(Comand.Halt);
        }

        public static Operation Refer(Symbol var, Operation next) {
            return new Operation(Comand.Refer) {Var = var, Branch1 = next};
        }

        public static Operation Constant(object obj, Operation next) {
            return new Operation(Comand.Constant) {Value = obj, Branch1 = next};
        }

        public static Operation Close(Symbol[] vars, Operation body, Operation next) {
            return new Operation(Comand.Close) {Vars = vars, Branch2 = body, Branch1 = next};
        }

        public static Operation Test(Operation thenc, Operation elsec) {
            return new Operation(Comand.Test) {Branch1 = thenc, Branch2 = elsec};
        }

        public static Operation Extend(Symbol var, Operation next) {
            return new Operation(Comand.Extend) {Var = var, Branch1 = next};
        }

        public static Operation Assign(Symbol var, Operation next) {
            return new Operation(Comand.Assign) {Var = var, Branch1 = next};
        }

        public static Operation Conti(Operation next) {
            return new Operation(Comand.Conti) {Branch1 = next};
        }

        public static Operation Nuate(object stack, Symbol var) {
            return new Operation(Comand.Nuate) {Var = var, Value = stack};
        }

        public static Operation Frame(Operation next, Operation ret) {
            return new Operation(Comand.Frame) {Branch1 = next, Branch2 = ret};
        }

        public static Operation Argument(Operation next) {
            return new Operation(Comand.Argument) {Branch1 = next};
        }

        public static Operation Apply() {
            return new Operation(Comand.Apply);
        }

        public static Operation Return() {
            return new Operation(Comand.Return);
        }
    }
}
