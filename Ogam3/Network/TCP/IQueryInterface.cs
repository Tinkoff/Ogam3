using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ogam3.Lsp.Generators;

namespace Ogam3.Network.TCP {
    [Enviroment("<o3-internal>")]
    public interface IQueryInterface {
        string[] GetIndexedSymbols();
    }
}
