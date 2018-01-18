using System;
using System.Collections.Generic;
using System.Linq;

namespace Ogam3.Lsp {
    public class Evaluator {

        public EnviromentFrame DefaultEnviroment = new Core();


        public object EvlString(string str) {
            return EvlString(str, DefaultEnviroment);
        }

        public object EvlSeq(Cons seq) {
            return EvlSeq(seq, DefaultEnviroment);
        }

        public object EvlString(string str, EnviromentFrame env) {
            object res = null;
            foreach (var exp in Reader.Read(str).GetIterator()) {
                res = Eval(exp.Car(), env);
            }

            return res;
        }

        public object EvlSeq(Cons seq, EnviromentFrame env) {
            object res = null;
            foreach (var exp in seq.GetIterator()) {
                res = Eval(exp.Car(), env);
            }

            return res;
        }

        static dynamic Eval(dynamic exp, EnviromentFrame env) {
            start:

            if (exp == null) return null;

            if (exp.GetType() != typeof(Cons)) { // apply, self quote
                if (exp is Symbol) return env.Get((Symbol) exp);

                return exp;
            }

            // special forms and eval
            Cons form = (Cons) exp;
            dynamic val = null;

            var op = (Symbol) form.Car();
            Cons arguments = (Cons) form.Cdr();

            if (op != null) {
                switch (op.ToString()) {
                    case "quote":
                        return arguments.Car();
                    case "if":
                        if (GetSBool(Eval(arguments.Car(), env)) == false) {
                            //********* tail call ***********
                            exp = arguments.Cdr().Cdr().Car();
                            goto start;
                            //*******************************
                        }

                        //********* tail call ***********
                        exp = arguments.Cdr().Car(); // tail call
                        goto start;
                        //*******************************
                    case "begin":
                        foreach (var itm in (arguments.GetIterator()).EmptyIfNull()) {
                            if (itm.Cdr() == null) { // test last item
                                //********* tail call ***********
                                exp = itm.Car();
                                goto start;
                                //*******************************
                            }

                            Eval(itm.Car(), env);
                        }

                        break;
                    case "define": {
                        var firstArg = arguments.Car();
                        if (firstArg is Symbol) { // variable
                            env.Define((Symbol) firstArg, Eval(arguments.Cdr().Car(), env));
                        } else if (firstArg is Cons) { // function
                            env.Define((Symbol) firstArg.Car(), MakeLambda(new Cons(firstArg.Cdr() , arguments.Cdr()), env));
                        }
                        return null;
                    }
                    case "set!":
                        env.Set((Symbol) arguments.Car(), Eval(arguments.Cdr().Car(), env));
                        return null;
                    case "lambda":
                        return MakeLambda(arguments, env);

                    case "call/cc": {
                        var freeze = new Continuation(form.ToString());

                        var continuation = new Func<Params, dynamic>((par) => {
                            val = par[0];
                            throw freeze;
                        });

                        var lmb = MakeLambda((Cons) arguments.Car().Cdr(), env);

                        var seq = new Cons(lmb, new Cons(continuation));

                        try {
                            return Eval(seq, env);
                        }
                        catch (Exception expt) {
                            if (freeze == expt.InnerException) return val;
                            throw expt;
                        }
                    }
                }
            }

            // APPLY
            var candidateToCall = Eval(form.Car(), env);

            var args = new Queue<object>();

            if (arguments != null) {
                foreach (var arg in (arguments.GetIterator()).EmptyIfNull()) {
                    args.Enqueue(Eval(arg.Car(), env));
                }
            }

            if (candidateToCall is Lambda) {
                var lmbd = candidateToCall as Lambda;
                var argCnt = 0;
                var callEnv = new EnviromentFrame(lmbd.Closure);

                foreach (var arg in lmbd.Argument.GetIterator()) {
                    if (!args.Any()) break;
                    callEnv.Define((Symbol) arg.Car(), Eval(args.Dequeue(), env));
                    argCnt++;
                }

                if (lmbd.Arity == argCnt) {
                    env = callEnv;

                    foreach (var itm in lmbd.Body) {
                        if (itm.Cdr() == null) { // test last item
                            //********* tail call ***********
                            exp = itm.Car();
                            goto start;
                            //*******************************
                        }
                        
                        Eval(itm.Car(), callEnv);
                    }
                }
                else {
                    throw new Exception($"Arity mismatch, expected {lmbd.Arity}, given {argCnt} arguments");
                }
            }
            else if (candidateToCall is MulticastDelegate) {
                var mcd = candidateToCall as MulticastDelegate;
                var parameters = mcd.Method.GetParameters();

                var cArg = new List<object>();
                foreach (var pi in parameters) {
                    if (typeof(Params) == pi.ParameterType) {
                        var par = new Params();
                        while (args.Any()) {
                            par.Add(args.Dequeue());
                            
                        }
                        cArg.Add(par);
                    }
                    else {
                        if (args.Any()) {
                            cArg.Add(args.Dequeue());
                        }
                    }
                }


                if (parameters.Length == cArg.Count) {
                    exp = mcd.DynamicInvoke(cArg.ToArray());
                    if (exp is TailCall) { // A function may be doing tail call too
                        //********* tail call ***********
                        exp = (exp as TailCall).Exp;
                        goto start;
                        //*******************************
                    }

                    return exp;
                }
                else {
                    throw new Exception($"Arity mismatch {mcd}, expected {parameters.Length}, given {cArg.Count} arguments");
                }
            }

            throw new Exception("Application: not a procedure");
        }

        public static Lambda MakeLambda(Cons form, EnviromentFrame clojure) {
            if (!(form.Car() is Cons)) throw new Exception($"lambda: bad argument sequence {ExtendObj.Car(form)}");

            return new Lambda((Cons) form.Car(), clojure, (Cons) form.Cdr());
        }

        public static bool GetSBool(object o) {
            if (o == null)
                return false;
            if (o is bool)
                return (bool) o;
            else
                return true;
        }
    }
}
