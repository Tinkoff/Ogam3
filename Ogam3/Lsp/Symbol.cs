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
