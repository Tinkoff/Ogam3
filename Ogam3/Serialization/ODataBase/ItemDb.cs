using System;
using System.Collections.Generic;
using System.IO;
using Ogam3.Lsp;
using Ogam3.Utils;

namespace Ogam3.Serialization.ODataBase {
    public class ItemDb {
        public string Id;
        public string ObjectId;
    }

    public class ValueDb : ItemDb {
        public object Value;
        public ValueTypeE ValueType;

        public ValueDb() { }

        public ValueDb(object value) {
            Id = SGuid.GetSSGuid();
            _typeSwitch.TryGetValue(value.GetType(), out ValueType);

            Value = value is Symbol ? value.ToString() : value;
        }

        private static Dictionary<Type, ValueTypeE> _typeSwitch = new Dictionary<Type, ValueTypeE>() {
            { typeof(Int16), ValueTypeE.Int16 },
            { typeof(UInt16), ValueTypeE.UInt16 },
            { typeof(Int32), ValueTypeE.Int32 },
            { typeof(UInt32), ValueTypeE.UInt32 },
            { typeof(Int64), ValueTypeE.Int64 },
            { typeof(UInt64), ValueTypeE.UInt64 },
            { typeof(Single), ValueTypeE.Float32 },
            { typeof(Double), ValueTypeE.Float64 },
            { typeof(Byte), ValueTypeE.Byte },
            { typeof(Boolean), ValueTypeE.Boolean },
            { typeof(String), ValueTypeE.String },
            { typeof(Symbol), ValueTypeE.Symbol },
            { typeof(DateTime), ValueTypeE.DateTime },
            { typeof(MemoryStream), ValueTypeE.Blob },
        };

        public enum ValueTypeE {
            Undef,
            Int16, UInt16, Int32, UInt32, Int64, UInt64, Float32, Float64, Byte,
            Boolean,
            String,
            Symbol,
            DateTime,
            Blob
        }

        public bool IsString() {
            return ValueType == ValueTypeE.String;}
        public bool IsSymbol() {
            return ValueType == ValueTypeE.Symbol;}

        public bool IsVarChar() {
            return IsString() || IsSymbol();}

        public bool IsBool() {
            return ValueType == ValueTypeE.Boolean;}

        public bool IsDateTime() {
            return ValueType == ValueTypeE.DateTime;}

        public bool IsBlob() {
            return ValueType == ValueTypeE.Blob;}

        public bool IsNumber() {
            switch (ValueType) {
                case ValueTypeE.Int16:
                case ValueTypeE.UInt16:
                case ValueTypeE.Int32:
                case ValueTypeE.UInt32:
                case ValueTypeE.Int64:
                case ValueTypeE.UInt64:
                case ValueTypeE.Float32:
                case ValueTypeE.Float64:
                case ValueTypeE.Byte:
                case ValueTypeE.Boolean:
                    return true;}
            return false;
        }
    }

    public class ConsDb : ItemDb {
        public string CarId;
        public string CdrId;

        public ConsDb() { }

        public ConsDb(string carId, string cdrId) {
            Id = SGuid.GetSSGuid();
            CarId = carId;
            CdrId = cdrId;
        }
    }

}
