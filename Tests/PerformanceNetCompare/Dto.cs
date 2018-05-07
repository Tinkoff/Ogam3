using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceNetCompare {
    [DataContract]
    public class Dto {
        [DataMember] public string StringMember { get; set; }
        [DataMember] public int IntMember { get; set; }
        [DataMember] public bool BoolMember { get; set; }
        [DataMember] public DateTime DateTimeMember { get; set; }

        public static Dto GetObject() {
            var dto = new Dto();
            dto.StringMember = "String data";
            dto.IntMember = 1133;
            dto.BoolMember = true;
            dto.DateTimeMember = DateTime.Now;

            return dto;
        }
    }
}
