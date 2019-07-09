using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ogam3.Utils;

namespace Ogam3.Lsp {
    public class MacroSystem {
        public EnviromentFrame<SyntaxRules> Macro = new EnviromentFrame<SyntaxRules>();

        private void DefineMacro(Cons cons) {
            var macroName = cons.Car() as Symbol;
            var macro = cons.Cdr().Car() as Cons;

            if ((macro.Car() as Symbol)?.Name == "syntax-rules") {
                var syntaxRules = BuildSynataxRules(macro.Cdr() as Cons);
                Macro.Define(macroName, syntaxRules);
            } else {
                // unsuported
            }
        }

        private static SyntaxRules BuildSynataxRules(Cons macro) {
            var literals = macro.Car() as Cons;
            var clause = macro.Cdr() as Cons;
            var ruleItems = new List<SyntaxRuleItem>();
            foreach (Cons rule in clause.GetIterator()) {
                ruleItems.Add(BuildSynataxRule(rule.Car() as Cons));
            }

            return new SyntaxRules() { Rules = ruleItems.ToArray() };
        }

        private static SyntaxRuleItem BuildSynataxRule(Cons macro) {
            var name = macro.Car().Car() as Symbol;
            var args = ReadArguments(macro.Car().Cdr());
            var body = macro.Cdr() as Cons;
            var prepared = PrepareBody(args, body);

            return new SyntaxRuleItem() { Body = prepared, Name = name, Args = macro.Car().Cdr() as Cons};
        }

        private static string _ellipses = "...";

        private static SyntaxArgument[] ReadArguments(object cons) {
            var args = new List<SyntaxArgument>();
            var itm = cons;
            while (true) {
                if (itm is Cons) {
                    var arg = itm.Car();
                    if (arg is Cons) {
                        args.AddRange(ReadArguments(arg));
                    } else if (arg is Symbol) {
                        var sym = arg as Symbol;
                        if (sym.Name != _ellipses) { // exclude ellipse
                            args.Add(new SyntaxArgument(sym));
                        }
                    }
                    else {
                        throw new Exception("wrong syntax");
                    }

                    itm = itm.Cdr();
                } else if (itm is Symbol) {
                    args.Add(new SyntaxArgument(itm as Symbol, true));
                    itm = null;
                } else {
                    break;
                    // error
                }
            }

            return args.ToArray();
        }

        static Cons PrepareBody(SyntaxArgument[] args, Cons body) {
            var item = body;
            while (item != null) {
                var symbol = item.Car() as Symbol;

                if (symbol != null) {
                    //if (symbol.Name == "lambda") {
                    //    var largs = (item.Cdr().Car() as Cons)?.GetIterator().Select(a => (a.Car() as Symbol)?.Name)
                    //        .ToArray();
                    //    var lbinds = args.Where(a => !largs.Any(la => la == a.Original.Name)).ToArray();
                    //    PrepareBody(lbinds, item.Cdr().Cdr() as Cons);
                    //    break;
                    //}

                    if (symbol.Name == "quote") {
                        // ignore quoted
                        break;
                    } else {
                        var syntax = args.FirstOrDefault(s => symbol.Name == s.Original.Name);
                        if (syntax != null) {
                            ((Cons)item).SetCar(syntax);
                        }
                    }
                } else {
                    var form = item.Car() as Cons;
                    if (form != null) {
                        PrepareBody(args, form);
                    }
                }

                var next = item.Cdr();

                if (next is Cons) {
                    item = next as Cons;
                } else {
                    symbol = item.Cdr() as Symbol;
                    if (symbol != null) {
                        var syntax = args.FirstOrDefault(s => symbol.Name == s.Original.Name);
                        if (syntax != null) {
                            ((Cons)item).SetCdr(syntax);
                        }
                    }

                    item = null;
                }
            }

            return body;
        }

        public Cons MacroProcessing(Cons seq) { // TODO
            var rseq = new Cons();
            foreach (var o in seq.GetIterator()) {
                var exp = o.Car();

                if (exp is Cons) {
                    var op = exp.Car() as Symbol;
                    var arguments = exp.Cdr() as Cons;

                    if (op?.Name == "define-syntax") {
                        DefineMacro(arguments);
                        continue;

                    } else if (Macro.Lookup(op)) {
                        foreach (var o1 in (Macro.Get(op).Expand(arguments)).GetIterator()) {
                            if (o1.Car() is Cons) {
                                rseq.Add(MacroProcessing(o1.Car()));
                            }
                            else {
                                rseq.Add(o1.Car());
                            }
                        }

                        continue;
                    }

                    rseq.Add(MacroProcessing(exp));
                }
                else {
                    rseq.Add(exp);
                }
            }
            return rseq;
        }
    }

    public class SyntaxRules {
        public Symbol[] Symbols;
        public SyntaxRuleItem[] Rules;

        public Cons Expand(Cons values) {
            var ruleWithBind = Math(values);

            if (ruleWithBind == null) {
                throw new Exception("Macro arity mismatch");
            }

            var res = ruleWithBind.Item1.Expand(ruleWithBind.Item2);

            return res;
        }

        static Dictionary<string, object> Bind(Cons args, Cons values) {
            var bind = new Dictionary<string, object>();
            object a;
            object b;
            var isEllipseMode = false;

            void SetBind(string key, object value) {
                if (bind.TryGetValue(key, out var oo)) {
                    if (oo is Cons) {
                        var seq = oo as Cons;
                        seq.Add(value);
                        bind[key] = seq;
                    }
                    else {
                        throw new Exception("error");
                    }
                }
                else {
                    if (isEllipseMode) {
                        bind[key] = new Cons(value);
                    }
                    else {
                        bind[key] = value;
                    }
                }
            }

            while (true) {
                a = args?.Car();
                b = values?.Car();

                isEllipseMode = (args?.Cdr().Car() as Symbol)?.Name == "...";

                if ((a == null && b == null) || (isEllipseMode && b == null)) {
                    return bind;
                }

                if (a is Symbol && b != null) {
                    var sym = a as Symbol;
                    SetBind(sym.Name, b);
                } else if (a is Cons && b is Cons) {
                    var bnd = Bind(a as Cons, b as Cons);
                    foreach (var pair in bnd) {
                        SetBind(pair.Key, pair.Value);
                    }
                } else {
                    return null;
                }

                var nexA = args.Cdr();

                if (nexA is Symbol) { // rest
                    var lst = new Cons();

                    values = values.Cdr() as Cons;

                    while (values != null) {
                        b = values?.Car();

                        lst.Add(b);

                        values = values.Cdr() as Cons;
                    }

                    SetBind(((Symbol) nexA).Name, lst);
                }

                values = values?.Cdr() as Cons;
                var nextCons = nexA as Cons;

                var nextSym = nextCons?.Car() as Symbol;
                if (nextSym?.Name == "...") { // repeat for ...
                    if (values == null) { // complete
                        return bind;
                    }
                }
                else {
                    args = nextCons;
                }
            }
        }

        public Tuple<SyntaxRuleItem, Dictionary<string, object>> Math(Cons values) {
            foreach (var syntaxRuleItem in Rules) {
                var bids = Bind(syntaxRuleItem.Args, values);
                if (bids != null) {
                    return new Tuple<SyntaxRuleItem, Dictionary<string, object>>(syntaxRuleItem, bids);
                }
            }

            return null;
        }
    }

    public class SyntaxRuleItem {
        public Symbol Name;
        public Cons Body;
        public Cons Args;

        public Cons Expand(Dictionary<string, object> binds) {
            return Expand(binds, Body);
        }

        static Cons Expand(Dictionary<string, object> binds, Cons body) { // TODO bind val && arg
            var newBody = new Cons();
            while (body != null) {
                var syntax = body.Car() as SyntaxArgument;
                if (syntax != null) {
                    var nextSym = body.Cdr().Car() as Symbol;
                    if (nextSym != null && nextSym.Name == "...") {
                        if (binds.TryGetValue(syntax.Original.Name, out var val)) {
                            if (newBody.Car() == null && newBody.Cdr() == null && val is Cons) {
                                newBody = binds[syntax.Original.Name] as Cons; // TODO not good
                            }
                            else {
                                newBody.Add(binds[syntax.Original.Name], true);
                            }
                        }

                        body = body.Cdr() as Cons; // skip ellipse
                    }
                    else {
                        newBody.Add(binds[syntax.Original.Name]);
                    }
                } else {
                    var form = body.Car() as Cons;
                    if (form != null) {
                        newBody.Add(Expand(binds, form));
                    } else {
                        newBody.Add(body.Car());
                    }
                }

                var next = body.Cdr();

                if (next is Cons) {
                    body = body.Cdr() as Cons;
                } else if (next is SyntaxArgument) { // rest
                    syntax = next as SyntaxArgument;

                    newBody.Add(binds[syntax.Original.Name], true);

                    body = null;
                } else {
                    body = null;
                }
            }

            return newBody;
        }
    }

    public class SyntaxArgument {
        public Symbol Replaced;
        public Symbol Original;
        public bool IsRest;

        public SyntaxArgument(Symbol name, bool isRest = false) {
            Original = name;
            IsRest = isRest;
            Replaced = new Symbol($"{name.Name}-{SGuid.ShortUID()}");
        }
        public override string ToString() {
            return IsRest ? $". {Original}" : Original.ToString();
        }
    }
}
