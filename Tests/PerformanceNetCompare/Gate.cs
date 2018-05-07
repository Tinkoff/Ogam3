using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceNetCompare {
    public class Gate : IGate {
        public Dto Echo(Dto dto) {
            return dto;
        }
    }
}
