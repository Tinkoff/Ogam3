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
using System.Diagnostics;
using Ogam3.Frt;

namespace TestForth {
    class Program {
        public static OForth Interpreter;
        static void Main(string[] args) {
            Interpreter = new OForth();
            TestLoopSpeed();
            ReflectionTest();

            Console.ReadLine();
        }

        static void TestLoopSpeed(int loops = 1000000) {
            Console.WriteLine($"Start test of loop speed, {loops} iterations");
            var sw = new Stopwatch();
            sw.Start();

            Interpreter.Eval($": while-tst begin dup 0 > while 1 - repeat ; s\" start while-loop: \" . {loops} dup . cr while-tst s\" end of loop\" . cr");

            sw.Stop();
            Console.WriteLine($"End test of loop speed, {loops} iterations in {sw.Elapsed}");
        }

        static void ReflectionTest() {
            // Create new instance
            Interpreter.Eval($"word {typeof(ReflectionTestClass).FullName} type new");
            // Print object
            Interpreter.Eval($"s\" An instance of the \" . dup word {nameof(ReflectionTestClass.ToString)} swap m@ invk . bl s\" was created\" . cr");

            // Nested value access
            var nestedFieldName = $"{nameof(ReflectionTestClass.NestedObject)}.{nameof(ReflectionTestClass.NestedObject.SomeValue)}";
            var printNestedValue = $"dup word {nestedFieldName} dup . bl s\" = \" . swap m@ . cr";
            Interpreter.Eval(printNestedValue);
            Interpreter.Eval($"s\" Change value\" . cr");
            Interpreter.Eval($" dup dup word {nestedFieldName} swap m@ dup * swap word {nestedFieldName} swap m!");
            Interpreter.Eval(printNestedValue);

            Interpreter.Eval($"s\" Nested item access:\" . cr");
            Interpreter.Eval($"dup word {nameof(ReflectionTestClass.NestedObjectArray)}[0] swap m@ . cr");
            Interpreter.Eval($"dup word {nameof(ReflectionTestClass.NestedObjectArray)}[1] swap m@ . cr");

            Interpreter.Eval($"drop .s"); // drop instance

            Interpreter.Eval($"s\" Static field access:\" . cr");
            var staticField = $"word {nameof(ReflectionTestClass.StaticStringField)} word {typeof(ReflectionTestClass).FullName} type";
            var printStaticValue = $"{staticField} ms@ . cr";
            Interpreter.Eval(printStaticValue);
            Interpreter.Eval($"s\" new static string value\" {staticField} ms! ");
            Interpreter.Eval(printStaticValue);
        }
    }

    public class ReflectionTestClass {
        public string StringField;
        public static string StaticStringField = "Init value";
        public static string[] StaticStringArray = new[] { "Init one value", " Init two value", "Init three value" };

        public ReflectionTestSubClass NestedObject = new ReflectionTestSubClass();
        public ReflectionTestSubClass[] NestedObjectArray = new ReflectionTestSubClass[] { new ReflectionTestSubClass() {SomeValue = 1.0}, new ReflectionTestSubClass() { SomeValue = 2.0 } };
    }

    public class ReflectionTestSubClass {
        public double SomeValue = 11.33;

        public override string ToString() {
            return $"{nameof(ReflectionTestSubClass)}.{nameof(SomeValue)} = {SomeValue}";
        }
    }
}
