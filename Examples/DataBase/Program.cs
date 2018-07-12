using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ogam3.Lsp;
using Ogam3.Serialization;
using Ogam3.Serialization.ODataBase;
using Ogam3.Utils;

namespace DataBase {
    class Program {
        static void Main(string[] args) {
            var originalObject = new StoreObject() {
                DateTimeValue = DateTime.Now,
                DoubleValue = 11.33,
                IntegerValue = 1133,
                StringValue = "String message",
                StringList = new string[100].Select(s => SGuid.ShortUID()).ToList(),
                StreamValue = new MemoryStream(File.ReadAllBytes(Directory.GetFiles(".").First()))
            };

            var objectId = $"ID-{SGuid.ShortUID()}";

            var db = new SqliteStorage("obj_storage.db", "O3");
            db.InitDb();

            Console.WriteLine("Insert...");
            db.Insert(originalObject, objectId);

            Console.WriteLine("Upsert...");
            db.Upsert(originalObject, objectId);

            Console.WriteLine("Select...");
            var recoveryObject = db.Select<StoreObject>(objectId);

            Console.WriteLine("Delete...");
            db.Delete(objectId);

            Console.WriteLine($"Success!");
            Console.ReadLine();
        }
    }
}
