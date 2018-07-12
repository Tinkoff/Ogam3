/*
 * Copyright © 2018 Tinkoff Bank
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using Ogam3.Lsp;
using Ogam3.Serialization;
using Ogam3.Serialization.ODataBase;

namespace Ogam3.Utils {
    public abstract class DbObjectBase {
        protected string _connectionString;
        protected string _storageName;

        protected DbObjectBase(string connectionString, string storageName) {
            _connectionString = connectionString;
            _storageName = storageName;
        }

        protected abstract DbConnection Connect();
        public abstract void Bootstrap();
        public abstract void Insert(object dbObject, string objectId);
        public abstract T Select<T>(string objectId);
        public abstract void Delete(string objectId);
        public abstract void Upsert(object dbObject, string objectId);

        protected static ItemDb[] Serialize(object dbObject, string objectId) {
            return MorpherDb.Sequence(OSerializer.SerializeOnly(dbObject), objectId).ToArray();
        }

        protected static T Deserialize<T>(ItemDb[] seq) {
            return (T) OSerializer.Deserialize(MorpherDb.Chain(seq.ToList()) as Cons, typeof(T));
        }

        protected object ReadValue(DbDataReader reader, int ordinal, ValueDb.ValueTypeE valueType) {
            if (reader.IsDBNull(ordinal)) return null;

            switch (valueType) {
                case ValueDb.ValueTypeE.Int16:
                    return reader.GetInt16(ordinal);
                case ValueDb.ValueTypeE.UInt16:
                    return (UInt16)reader.GetInt32(ordinal);
                case ValueDb.ValueTypeE.Int32:
                    return reader.GetInt32(ordinal);
                case ValueDb.ValueTypeE.UInt32:
                    return (UInt32)reader.GetInt64(ordinal);
                case ValueDb.ValueTypeE.Int64:
                    return reader.GetInt32(ordinal);
                case ValueDb.ValueTypeE.Float32:
                    return reader.GetFloat(ordinal);
                case ValueDb.ValueTypeE.Float64:
                    return reader.GetDouble(ordinal);
                case ValueDb.ValueTypeE.String:
                    return reader.GetValue(ordinal).ToString();
                case ValueDb.ValueTypeE.Symbol:
                    return new Symbol(reader.GetString(ordinal));
                case ValueDb.ValueTypeE.DateTime:
                    return reader.GetDateTime(ordinal);
                case ValueDb.ValueTypeE.Byte:
                    return reader.GetByte(ordinal);
                case ValueDb.ValueTypeE.Blob:
                    return GetStream(reader, ordinal);
            }

            return null;
        }

        protected abstract Stream GetStream(DbDataReader reader, int ordinal);

        protected static string GetStringValue(DbDataReader reader, string field) {
            try {
                int idx = reader.GetOrdinal(field);
                if (!reader.IsDBNull(idx))
                    return reader.GetValue(idx).ToString();
                return null;
            } catch (Exception) {
                return null;
            }
        }
    }
}
