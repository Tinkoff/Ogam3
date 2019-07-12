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
using Ogam3.Lsp.Defaults;

namespace Ogam3.Lsp {
    public class Evaluator {

        public EnviromentFrame DefaultEnviroment = new Core();
        public MacroSystem MacroSystem = new CoreMacro();


        public object EvlString(string str, bool isWithMacro = true) {
            return EvlString(str, DefaultEnviroment, isWithMacro);
        }

        public object EvlSeq(Cons seq, bool isWithMacro = true) {
            return EvlSeq(seq, DefaultEnviroment, isWithMacro);
        }

        public object EvlExp(Cons seq, bool isWithMacro = true) {
            return EvlSeq(new Cons(seq), DefaultEnviroment, isWithMacro);
        }

        public object EvlString(string str, EnviromentFrame env, bool isWithMacro = true) {
            if (string.IsNullOrWhiteSpace(str)) return null;
            return EvlSeq(Reader.Read(str), env, isWithMacro);
        }

        public object EvlSeq(Cons seq, EnviromentFrame env, bool isWithMacro = true) {
            return VirtualMashine.Eval(isWithMacro ? Compiler.Compile(MacroSystem.MacroProcessing(seq)) : Compiler.Compile(seq), env);
        }

        public object ApplyClosure(VirtualMashine.Closure closure, params object[] args) {
            return EvlSeq(new Cons(Cons.List(new object[] { closure }.Concat(args ?? new object[0]).ToArray())), false);
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
