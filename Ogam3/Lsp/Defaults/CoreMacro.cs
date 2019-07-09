using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ogam3.Lsp.Defaults {
    public class CoreMacro : MacroSystem {
        public CoreMacro() {
            var whenMacro = "(define-syntax when" +
                            "(syntax-rules ()" +
                            "((_ condition body ...) (if condition (begin body ...) #f))))";
            MacroProcessing(Reader.Read(whenMacro));

            var unlessMacro = "(define-syntax unless" +
                            "(syntax-rules ()" +
                            "((_ condition body ...) (if (not condition) (begin body ...) #f))))";
            MacroProcessing(Reader.Read(unlessMacro));

            var letMacro = "(define-syntax let" +
                           "(syntax-rules ()" +
                           "((_ ((name val) ...) body ...)" +
                           "((lambda (name ...) body ...) val ...))))";
            MacroProcessing(Reader.Read(letMacro));

            var condMacro = "(define-syntax cond" +
                            "(syntax-rules ()" +
                            "((false) #f)" +
                            "((_ (test result ...) rest ...) (if test (begin result ...) (cond rest ...)) )";
            MacroProcessing(Reader.Read(condMacro));
        }
    }
}
