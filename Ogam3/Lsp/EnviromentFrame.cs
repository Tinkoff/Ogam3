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
    public class EnviromentFrame : IEnviromentFrame{
        public EnviromentFrame Parent;

        public Dictionary<string, dynamic> Variables;

        public EnviromentFrame() {
            Variables = new Dictionary<string, dynamic>();
        }

        public EnviromentFrame(EnviromentFrame parent) {
            Parent = parent;
            Variables = new Dictionary<string, dynamic>();
        }

        public void Define(Symbol ident, dynamic value) {
            Variables[ident.Name] = value;
        }

        public void Define(string ident, dynamic value) {
            Define(new Symbol(ident), value);
        }

        public void Set(Symbol ident, dynamic value) {
            if (Variables.ContainsKey(ident.Name)) {
                Variables[ident.Name] = value;
                return;
            }

            if (Parent == null)
                throw new Exception($"Undefined \"{ident.Name}\"");

            Parent.Set(ident, value);
        }

        public dynamic Get(Symbol ident) {
            dynamic res = null;

            if (Variables.TryGetValue(ident.Name, out res)) return res;

            if (Variables.ContainsKey(ident.Name))
                return Variables[ident.Name];

            if (Parent == null)
                throw new Exception($"Undefined \"{ident.Name}\"");

            return Parent.Get(ident);
        }

        public bool Lookup(Symbol ident) {
            if (Variables.ContainsKey(ident.Name))
                return true;

            return Parent != null && Parent.Lookup(ident);
        }
    }

    public interface IEnviromentFrame {
        void Define(string ident, dynamic value);
    }
}
