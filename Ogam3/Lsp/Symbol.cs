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

namespace Ogam3.Lsp {
    public class Symbol {
        public string Name;

        public Symbol() { }

        public Symbol(string name) {
            Name = name;
        }

        public override string ToString() {
            return Name;
        }

        public override bool Equals(object obj) {
            var symbol = obj as Symbol;

            if (symbol == null)
                return false;

            return Name == symbol.Name;
        }

        public static bool operator ==(Symbol a, Symbol b) {
            if (ReferenceEquals(a, b)) {
                return true;
            }

            if (((object)a == null) || ((object)b == null)) {
                return false;
            }

            return a.Name == b.Name;
        }

        public static bool operator !=(Symbol a, Symbol b) {
            return !(a == b);
        }
    }
}
