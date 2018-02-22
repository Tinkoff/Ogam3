using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ogam3.Lsp;

namespace Ogam3 {
    public static class StingExtension {
        public static Evaluator Evaluator = new Evaluator(); // default evaluator
        public static Symbol O3Symbol(this string s) {
            return new Symbol(s);
        }

        public static object O3Eval(this string expr, params object[] args) => expr.O3Eval(Evaluator, args);

        public static object O3Eval(this string expr, Evaluator evaluator, params object[] args) {
            if (string.IsNullOrWhiteSpace(expr)) return null;


            return (args?.Any() ?? false) ? evaluator.EvlString(string.Format(expr, O2Strings(args))) : evaluator.EvlString(expr);
        }

        private static object[] O2Strings(object[] args) {
            var strs = new List<string>();
            foreach (var o in args) {
                strs.Add(Cons.O2String(o));
            }

            return strs.ToArray();
        }

        public static void O3Extend(this string name, dynamic call) {
            O3Extend(name, Evaluator, call);
        }

        public static void O3Extend(this string name, Evaluator evaluator, dynamic call) {
            name = name.Trim();

            if (name.StartsWith("(") || name.EndsWith(")")) {
                throw new Exception(String.Format("BINDING ERROR: uncorrect name \"{0}\"", name));
            }

            lock (evaluator) {
                evaluator.DefaultEnviroment.Define(name, call);
            }
        }
    }
}
