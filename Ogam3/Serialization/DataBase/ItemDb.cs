using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ogam3.Utils;

namespace Ogam3.Serialization.DataBase {
    public class ItemDb {
        public string Id;
        public string ObjectId;
    }

    public class ValueDb : ItemDb {
        public object Value;

        public ValueDb(object value) {
            Id = SGuid.GetSSGuid();
            Value = value;
        }
    }

    public class ConsDb : ItemDb {
        public string CarId;
        public string CdrId;

        public ConsDb(string carId, string cdrId) {
            Id = SGuid.GetSSGuid();
            CarId = carId;
            CdrId = cdrId;
        }
    }

}
