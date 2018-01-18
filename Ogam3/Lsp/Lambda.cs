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
            //Body[Body.Length - 1] = new TailCall(Body[Body.Length - 1]);
        }

        public Lambda() {
            Argument = new Cons();
            Closure = new EnviromentFrame();
            Body = new Cons[0];
        }
    }
}
