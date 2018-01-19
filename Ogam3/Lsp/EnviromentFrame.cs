using System;
using System.Collections.Generic;

namespace Ogam3.Lsp {
    public class EnviromentFrame {
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
}
