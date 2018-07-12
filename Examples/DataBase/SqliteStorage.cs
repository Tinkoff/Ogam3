using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ogam3.Lsp;
using Ogam3.Serialization.ODataBase;
using Ogam3.Utils;

namespace DataBase {
    public class SqliteStorage : DbObjectBase {
        string _dbName;
        public SqliteStorage(string databaseFile, string storageName) : base($"Data Source = {Path.GetFullPath(databaseFile)};", storageName) {
            _dbName = databaseFile;
        }

        struct DbNames {
            public const string TNumeric  = "NUMERIC";
            public const string TString   = "STRING";
            public const string TDateTime = "DATETIME";
            public const string TBlob     = "BLOB";
            public const string TCons     = "CONS";

            public static string[] TableTypes = { TNumeric, TString, TDateTime, TBlob };
            public static string[] AllTables = { TNumeric, TString, TDateTime, TBlob, TCons };

            public const string FValue     = "VALUE";
            public const string FValueType = "VALUE_TYPE";
            public const string FObjectId  = "OBJECT_ID";
            public const string FId        = "ID";
            public const string FCarId     = "CAR_ID";
            public const string FCdrId     = "CDR_ID";
        }

        public void InitDb() {
            var dbFileName = Path.GetFullPath(_dbName);
            if (!File.Exists(dbFileName)) {
                SQLiteConnection.CreateFile(dbFileName);
                Bootstrap();
            }
        }

        protected override DbConnection Connect() {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            return conn;
        }

        public override void Bootstrap() {
            using (var conn = Connect()) {
                using (var cmd = conn.CreateCommand()) {
                    cmd.Transaction = conn.BeginTransaction();
                    cmd.CommandType = System.Data.CommandType.Text;

                    foreach (var type in DbNames.TableTypes) {
                        cmd.CommandText =
                            $"CREATE TABLE {_storageName}_{type} ({DbNames.FId} VARCHAR(45) PRIMARY KEY UNIQUE NOT NULL, {DbNames.FObjectId}  VARCHAR(45) NOT NULL, {DbNames.FValue} {type}, {DbNames.FValueType} VARCHAR(45) NOT NULL)";
                        cmd.ExecuteNonQuery();
                    }

                    cmd.CommandText =
                        $"CREATE TABLE {_storageName}_{DbNames.TCons} ({DbNames.FId} VARCHAR(45) PRIMARY KEY UNIQUE NOT NULL, {DbNames.FObjectId} VARCHAR(45) NOT NULL, {DbNames.FCarId} VARCHAR(45), {DbNames.FCdrId} VARCHAR(45))";
                    cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
            }
        }

        public override void Insert(object dbObject, string objectId) {
            var items = Serialize(dbObject, objectId);
            using (var conn = Connect()) {
                using (var cmd = conn.CreateCommand()) {
                    cmd.Transaction = conn.BeginTransaction();
                    cmd.CommandType = System.Data.CommandType.Text;

                    foreach (var item in items.OfType<ValueDb>()) {
                        var type = GetType(item);
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(CreateParameter(DbNames.FId, item.Id));
                        cmd.Parameters.Add(CreateParameter(DbNames.FObjectId, item.ObjectId));
                        if (item.ValueType == ValueDb.ValueTypeE.Blob) {
                            cmd.Parameters.Add(CreateParameter(DbNames.FValue, (item.Value as MemoryStream).ToArray()));
                        } else {
                            cmd.Parameters.Add(CreateParameter(DbNames.FValue, item.Value));
                        }

                        cmd.Parameters.Add(CreateParameter(DbNames.FValueType, item.ValueType.ToString()));
                        cmd.CommandText = $"INSERT INTO {_storageName}_{type}({DbNames.FId},{DbNames.FObjectId},{DbNames.FValue},{DbNames.FValueType})VALUES(:{DbNames.FId},:{DbNames.FObjectId},:{DbNames.FValue},:{DbNames.FValueType})";
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var item in items.OfType<ConsDb>()) {
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(CreateParameter(DbNames.FId, item.Id));
                        cmd.Parameters.Add(CreateParameter(DbNames.FObjectId, item.ObjectId));
                        cmd.Parameters.Add(CreateParameter(DbNames.FCarId, item.CarId));
                        cmd.Parameters.Add(CreateParameter(DbNames.FCdrId, item.CdrId));
                        cmd.CommandText = $"INSERT INTO {_storageName}_{DbNames.TCons}({DbNames.FId},{DbNames.FObjectId},{DbNames.FCarId},{DbNames.FCdrId})VALUES(:{DbNames.FId},:{DbNames.FObjectId},:{DbNames.FCarId},:{DbNames.FCdrId})";
                        cmd.ExecuteNonQuery();
                    }


                    cmd.Transaction.Commit();
                }
            }
        }

        static object CreateParameter(string key, object value) {
            return new SQLiteParameter(key, value ?? DBNull.Value);
        }

        static string GetType(ValueDb item) {
            if (item.IsNumber()) return DbNames.TNumeric;
            if (item.IsVarChar()) return DbNames.TString;
            if (item.IsDateTime()) return DbNames.TDateTime;
            if (item.IsBlob()) return DbNames.TBlob;

            throw new Exception($"{item.ValueType} is unsupported.");
        }

        public override T Select<T>(string objectId) {
            var items = new List<ItemDb>();
            using (var conn = Connect()) {
                using (var cmd = conn.CreateCommand()) {
                    cmd.Transaction = conn.BeginTransaction();
                    cmd.CommandType = System.Data.CommandType.Text;

                    foreach (var type in DbNames.TableTypes) {
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(CreateParameter(DbNames.FObjectId, objectId));
                        cmd.CommandText = $"SELECT * FROM {_storageName}_{type} WHERE {DbNames.FObjectId} = :{DbNames.FObjectId}";

                        using (var reader = cmd.ExecuteReader()) {
                            items.AddRange(ReadValues(reader, objectId));
                        }
                    }

                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(CreateParameter(DbNames.FObjectId, objectId));
                    cmd.CommandText = $"SELECT * FROM {_storageName}_{DbNames.TCons} WHERE {DbNames.FObjectId} = :{DbNames.FObjectId}";

                    using (var reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            items.Add(new ConsDb() {
                                Id = GetStringValue(reader, DbNames.FId),
                                ObjectId = objectId,
                                CarId = GetStringValue(reader, DbNames.FCarId),
                                CdrId = GetStringValue(reader, DbNames.FCdrId)
                            });
                        }
                    }

                    cmd.Transaction.Commit();
                }
            }

            return Deserialize<T>(items.ToArray());
        }

        IEnumerable<ValueDb> ReadValues(DbDataReader reader, string objectId) {
            while (reader.Read()) {
                ValueDb.ValueTypeE valueType;
                if (Enum.TryParse(GetStringValue(reader, DbNames.FValueType), out valueType)) {
                    var item = new ValueDb() {
                        Id = GetStringValue(reader, DbNames.FId),
                        ObjectId = objectId,
                        ValueType = valueType
                    };

                    var ordinal = reader.GetOrdinal(DbNames.FValue);

                    item.Value = ReadValue(reader, ordinal, valueType);

                    yield return item;
                }
            }
        }

        protected override Stream GetStream(DbDataReader reader, int ordinal) {
            using (var stream = reader.GetStream(ordinal)) {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }
        }

        public override void Delete(string objectId) {
            using (var conn = Connect()) {
                using (var cmd = conn.CreateCommand()) {
                    cmd.Transaction = conn.BeginTransaction();
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.Parameters.Add(CreateParameter(DbNames.FObjectId, objectId));

                    foreach (var type in DbNames.AllTables) {
                        cmd.CommandText = $"DELETE FROM {_storageName}_{type} WHERE {DbNames.FObjectId} = :{DbNames.FObjectId}";
                        cmd.ExecuteNonQuery();
                    }

                    cmd.Transaction.Commit();
                }
            }
        }

        public override void Upsert(object dbObject, string objectId) {
            Delete(objectId);
            Insert(dbObject, objectId);
        }
    }
}
