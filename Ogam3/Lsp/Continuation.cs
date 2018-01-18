using System;

namespace Ogam3.Lsp {
    public class Continuation : Exception {
        public Continuation(string message) {
            Message = message;
        }

        public override string Message { get; }

        public override string StackTrace => "";
    }
}
