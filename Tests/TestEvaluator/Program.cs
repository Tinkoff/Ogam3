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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ogam3;
using Ogam3.Lsp;
using Ogam3.Utils;

namespace TestEvaluator {
    class Program {
        static void Main(string[] args) { // TODO
            Macro();
            Base();
            IntrnalState();
            ThreadSafe();

            Console.WriteLine("Time meterings...");
            var maxI = 10000000;
            foreach (var timeMeteringExpression in TimeMeteringExpressions()) {
                var watcher = new System.Diagnostics.Stopwatch();
                watcher.Start();
                for (var i = 0; i < maxI; i++) {
                    timeMeteringExpression.O3Eval();
                }
                watcher.Stop();

                Console.WriteLine(watcher.Elapsed);
            }

            Console.WriteLine("successful");

            Console.ReadLine();
        }

        static IEnumerable<string> TimeMeteringExpressions() {
            "(display \"Metering a .net function call...\")".O3Eval();
            yield return "(+ 1 2 3)";

            "(display \"Metering a scheme function call...\")".O3Eval();
            "(define (some-function a b) #f)".O3Eval();
            yield return "(some-function 11 33)";

            "(display \"Metering a lamda create...\")".O3Eval();
            yield return "(lambda (a b c) (+ 1 2 3))";
        }

        static bool IntrnalState() {
            "(define (make-obj) (define message \"Init value...ok\" )(define (dispatch op) (if (eq? op 'test) (begin (display message) (newline)) (begin (set! message \"Test bad call...ok\") (display  \"Unknown operation: \") (display op) (display \"...ok\") (newline)))) dispatch)".O3Eval();

            "(define obj (make-obj))".O3Eval();

            "(obj 'test)".O3Eval();

            "(obj 'SomeOperation)".O3Eval();

            "(obj 'test)".O3Eval();

            "(define x 0) (define y 1)".O3Eval();
            "(display \"Init values: \")(display x) (display \" - \") (display y) (newline)".O3Eval();
            "(define (context y) (define x 3311) (display \"Overload values: \") (display x) (display \" - \") (display y) (newline))".O3Eval();
            "(context 1133)".O3Eval();
            "(display \"Recovery values: \")(display x) (display \" - \") (display y) (newline)".O3Eval();

            return true;
        }

        static bool Macro() {
            var whenRes = "(when (> 5 1) (when #t 100500))".O3Eval();
            var letRes = "(let ((a 1) (b 2)) (+ a b))".O3Eval();
            var condRes = "(cond (#f 1 2) (#t 3 4) (#t 5 6))".O3Eval();

            return true;
        }
        static bool Base() {
            object result = null;
            result = "'(a . b)".O3Eval();
            result = "(+ 111 222 333)".O3Eval();
            result = "(if #t 1 2)".O3Eval();
            result = "(if #f 1 2)".O3Eval();
            result = "(if 0 1 2)".O3Eval();

            result = "(if #t 1)".O3Eval();
            result = "(if #f 1)".O3Eval();
            result = "(if 0 1)".O3Eval();

            result = "\"some string\"".O3Eval();
            result = "132".O3Eval();
            result = "#\\q".O3Eval();
            result = "#\\#".O3Eval();
            result = "#\\'".O3Eval();
            result = "'(1 2 3)".O3Eval();
            result = "'1".O3Eval();
            result = "#(1 2 3)".O3Eval();

            result = "(car '(1 2 3))".O3Eval();
            result = "(cdr '(1 2 3))".O3Eval();

            result = "(set-car! '(1 2 3) 1133)".O3Eval();
            result = "(set-cdr! '(1 2 3) 1133)".O3Eval();

            result = "(read \"(1 2 3 (#t #f) and something else)\")".O3Eval();
            //result = "(eval (read \"(+ 1 2 3)\"))".O3Eval();
            //result = "(eval (read \"1133\"))".O3Eval();


            "test".O3Extend(new Func<object, object, string>(Test));
            result = "(test {0} {1})".O3Eval("qwe\"asd\" \\ ", true);

            "get-date".O3Extend(new Func<DateTime>(GetDate));
            result = "(test (get-date) #t)".O3Eval();
            result = "(- 3 2 1)".O3Eval();

            result = "(define (summ a b) (+ a b)) (summ (call/cc (lambda (cc) (display \"text\")(newline) (cc 1) 100)) 2)".O3Eval();
            //result = "(define *env* #f)(call/cc (lambda (cc) (begin (set! *env* cc) (display \"suka\")))) (*env* #t)".O3Eval();

            result = "((lambda () #t))".O3Eval();

            result = "(begin 1 2 3)".O3Eval();

            
            "(newline) (display \"Tail call test...\") (newline)".O3Eval();
            "(define (loop step expr) (display \"iteration: \") (display step) (newline) (if (> step 0) (begin (set! step (- step 1)) (expr) (loop step expr)) \"end of loop\"))".O3Eval();
            result = "(loop 10000 (lambda () #t))".O3Eval();

            return true;
        }

        static string Test(object a, object b) {
            return "ok";
        }

        static DateTime GetDate() {
            return DateTime.Now;
        }

        static bool ThreadSafe() {
            Console.Write("Test async a function calls...");
            "(define (summ a b) (+ a b))".O3Eval();

            ThreadPool.QueueUserWorkItem(ThreadEva);
            ThreadPool.QueueUserWorkItem(ThreadEva);
            ThreadPool.QueueUserWorkItem(ThreadEva);
            ThreadPool.QueueUserWorkItem(ThreadEva);

            for (var i = 0; i < 5000; i++) {
                Thread.Sleep(1);
            }

            isRun = false;

            Thread.Sleep(1000);

            Console.WriteLine(_isAsyncCallok ? "ok" : "error");

            return true;
        }

        static Random _rnd = new Random();

        private static bool isRun = true;
        private static bool _isAsyncCallok = true;

        static void ThreadEva(object o) {
            while (isRun) {
                var a = _rnd.Next(100000);
                var b = _rnd.Next(100000);
                var res = (int)string.Format("(summ {0} {1})", a, b).O3Eval();
                if (res == (a + b)) {
                    //Console.Write(".");
                    continue;
                }
                else {
                    Console.Write("#");
                    _isAsyncCallok = false;
                }

                //System.Threading.Thread.Sleep(1);
            }

        }
    }


    public static class StackExtensions
    {
        public static Stack<T> Clone1<T>(this Stack<T> original)
        {
            return new Stack<T>(new Stack<T>(original));
        }

        public static Stack<T> Clone2<T>(this Stack<T> original)
        {
            return new Stack<T>(original.Reverse());
        }

        public static Stack<T> Clone3<T>(this Stack<T> original)
        {
            var arr = original.ToArray();
            Array.Reverse(arr);
            return new Stack<T>(arr);
        }

        public static Stack<T> Clone4<T>(this Stack<T> original)
        {
            var arr = new T[original.Count];
            original.CopyTo(arr, 0);
            Array.Reverse(arr);
            return new Stack<T>(arr);
        }
    }
}

