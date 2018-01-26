using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ogam3.Lsp {
    public class SpecialMessage {
        public readonly string Message;

        public SpecialMessage(Exception ex) {
            Message = ex.Message;
        }

        public SpecialMessage(string msg) {
            Message = msg;
        }

        public override string ToString() {
            return Message;
        }
    }
}
