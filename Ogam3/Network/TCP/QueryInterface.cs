using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ogam3.Lsp;

namespace Ogam3.Network.TCP {
    public class QueryInterface : IQueryInterface{
        private readonly List<string> _symbols;
        private SymbolTable _symbolTable;

        public QueryInterface() {
            _symbols = new List<string>();
        }
        public string[] GetIndexedSymbols() {
            lock (_symbols) {
                return _symbols.ToArray();
            }
        }

        public SymbolTable GetSymbolTable() {
            return _symbolTable;
        }

        public void UpsertIndexedSymbols(string[] indexedSymbols) {
            lock (_symbols) {
                foreach (var symbol in indexedSymbols) {
                    if (!_symbols.Contains(symbol)) {
                        _symbols.Add(symbol);
                    }
                }
                _symbolTable = new SymbolTable(_symbols?.ToArray());
            }
        }
    }
}
