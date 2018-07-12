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
using System.IO;
using System.Linq;
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
