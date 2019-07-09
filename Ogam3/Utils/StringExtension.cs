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
using Ogam3.Lsp;

namespace Ogam3 {
    public static class StringExtension {
        public static Evaluator Evaluator = new Evaluator(); // default evaluator
        public static Symbol O3Symbol(this string s) {
            return new Symbol(s);
        }

        public static object O3Eval(this string expr, params object[] args) => expr.O3Eval(Evaluator, args);

        public static object O3Eval(this string expr, Evaluator evaluator, params object[] args) {
            if (string.IsNullOrWhiteSpace(expr)) return null;


            return (args?.Any() ?? false) ? evaluator.EvlString(string.Format(expr, O2Strings(args)), true) : evaluator.EvlString(expr, true);
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

        public static StringPack O3Pack(this string str) {
            return new StringPack(str);
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

    public class StringPack {
        public string Str;

        public StringPack(string str) {
            Str = str;
        }

        public override string ToString() {
            return Str;
        }
    }
}
