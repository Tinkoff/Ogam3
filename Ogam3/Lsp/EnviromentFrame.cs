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

namespace Ogam3.Lsp {
    public class EnviromentFrame : EnviromentFrame<dynamic> {
        public EnviromentFrame(EnviromentFrame parent) {
            Parent = parent;
            Variables = new Dictionary<string, dynamic>();
        }

        public EnviromentFrame() {
            Variables = new Dictionary<string, dynamic>();
        }
    }
    public class EnviromentFrame<T> : IEnviromentFrame<T>{
        public EnviromentFrame<T> Parent;

        public Dictionary<string, T> Variables;

        public EnviromentFrame() {
            Variables = new Dictionary<string, T>();
        }

        public EnviromentFrame(EnviromentFrame<T> parent) {
            Parent = parent;
            Variables = new Dictionary<string, T>();
        }

        public void Define(Symbol ident, T value) {
            Variables[ident.Name] = value;
        }

        public void Define(string ident, T value) {
            Define(new Symbol(ident), value);
        }

        public void Set(Symbol ident, T value) {
            if (Variables.ContainsKey(ident.Name)) {
                Variables[ident.Name] = value;
                return;
            }

            if (Parent == null)
                throw new Exception($"Undefined \"{ident.Name}\"");

            Parent.Set(ident, value);
        }

        public T Get(Symbol ident) {
            T res = default(T);

            if (Variables.TryGetValue(ident.Name, out res)) return res;

            if (Variables.ContainsKey(ident.Name))
                return Variables[ident.Name];

            if (Parent == null)
                throw new Exception($"Undefined \"{ident.Name}\"");

            return Parent.Get(ident);
        }

        public bool Lookup(Symbol ident) {
            if (ident == null) return false;

            if (Variables.ContainsKey(ident.Name))
                return true;

            return Parent != null && Parent.Lookup(ident);
        }
    }

    public interface IEnviromentFrame<T> {
        void Define(string ident, T value);
    }
}
