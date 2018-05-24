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
using System.Linq;

namespace Ogam3.Lsp {
    public class Lambda {
        public string Id = Guid.NewGuid().ToString("N");
        public Cons Argument;
        public Cons[] Body;
        public EnviromentFrame Closure;
        public int Arity;

        public Lambda(Cons arguments, EnviromentFrame clojure, Cons body) {
            Argument = arguments;
            Closure = clojure;
            Arity = arguments.Car() == null ? Arity = 0 : arguments.Count();

            Body = body.GetIterator().Select(subExp => (Cons) subExp).ToArray();
        }

        public Lambda() {
            Argument = new Cons();
            Closure = new EnviromentFrame();
            Body = new Cons[0];
        }
    }
}
