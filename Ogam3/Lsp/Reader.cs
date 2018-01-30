using System;
using System.Collections.Generic;
using System.Linq;

namespace Ogam3.Lsp {
    public static class Reader {
        private enum ReadState {
            Normal,
            String,
            Sharp,
            Charter,
            Comment
        }

        public static Cons Read(string str) {
            var stack = new Stack<dynamic>();
            var root = new Cons();
            stack.Push(root);
            var state = ReadState.Normal;
            var isDot = false; // подумать
            var word = "";

            var set = new Action<object>(o => {
                if (isDot) {
                    var cons = stack.Peek() as Cons;
                    if (cons != null) {
                        cons.SetCdr(o);
                    }
                    else {
                        Console.WriteLine("ERROR 39");
                    }
                    isDot = false;
                }
                else {
                    stack.Peek().Add(o);
                }
            });

            foreach (var c in str) {
                switch (state) {
                    case ReadState.Normal: {
                        if (c == '(') {
                            var nod = new Cons();
                            stack.Peek().Add(nod);
                            stack.Push(nod);
                        }
                        else if (c == ')') {
                            if (!string.IsNullOrWhiteSpace(word)) {
                                //if (isDot) {
                                //    var cons = stack.Peek() as Cons;
                                //    if (cons != null) {
                                //        cons.SetCdr(ParseSymbol(word));
                                //    }
                                //    else {
                                //        Console.WriteLine("ERROR 39");
                                //    }
                                //    isDot = false;
                                //}
                                //else {
                                //    stack.Peek().Add(ParseSymbol(word));
                                //}
                                set(ParseSymbol(word));
                                word = "";
                            }

                            stack.Pop();
                        }
                        else if (" \n\r\t".Any(ch => ch == c)) {
                            if (!string.IsNullOrWhiteSpace(word)) {
                                if (word == ".") {
                                    isDot = true;
                                }
                                else {
                                    stack.Peek().Add(ParseSymbol(word));
                                }

                                word = "";
                            }
                        }
                        else if (';' == c) {
                            state = ReadState.Comment;
                        }
                        else if (c == '\"') {
                            state = ReadState.String;
                        }
                        else if (c == '#') {
                            state = ReadState.Sharp;
                        }
                        else if (c == '\'') {
                            var nod = new Cons(new Symbol("quote"));
                            //stack.Peek().Add(nod);
                            set(nod);
                            stack.Push(nod);
                        }
                        else {
                            word += c;
                        }
                        break;
                    }
                    case ReadState.Comment: {
                        if ("\n\r".Any(ch => ch == c)) {
                            state = ReadState.Normal;
                        }
                        break;
                    }
                    case ReadState.Sharp: {
                        switch (c) {
                            case 'T':
                            case 't':
                                //stack.Peek().Add(true);
                                set(true);
                                state = ReadState.Normal;
                                break;
                            case 'F':
                            case 'f':
                                //stack.Peek().Add(false);
                                set(false);
                                state = ReadState.Normal;
                                break;
                            case '(': {
                                var nod = new Cons(new Symbol("vector"));
                                //stack.Peek().Add(nod);
                                set(nod);
                                stack.Push(nod);
                                state = ReadState.Normal;
                                break;
                            }
                            case '\\': // read charter
                                state = ReadState.Charter;
                                break;
                        }
                        break;
                    }
                    case ReadState.Charter: {
                        //stack.Peek().Add(c);
                        set(c);
                        state = ReadState.Normal;
                        break;
                    }
                    case ReadState.String: { // in string
                        if (c == '\"') {
                            //stack.Peek().Add(word);
                            set(word);
                            word = "";
                            state = ReadState.Normal;
                        }
                        else {
                            word += c;
                        }
                        break;
                    }
                }
            } // forech c

            if (!string.IsNullOrWhiteSpace(word)) {
                set(state == ReadState.String ? word : ParseSymbol(word));
                //stack.Peek().Add(state == ReadState.String ? word : ParseSymbol(word));
            }

            return root;
        }

        static bool IsDigit(char c) {
            return (c >= '0' && c <= '9');
        }

        static object ParseSymbol(string str) {
            if (str.Length > 0) {
                if (IsDigit(str[0]) || (str.Length >= 2 && (str[0] == '+' || str[0] == '-') && IsDigit(str[1]))) {
                    return ParseNumber(str);
                }
                else {
                    return new Symbol(str);
                }
            }

            return null;
        }

        private static object ParseNumber(string str) {
            var factor = 1.0;
            var sign = 1;
            if (str[0] == '-') {
                sign = -1;
                str = str.Remove(0, 1);
            }
            else if (str[0] == '+') {
                str = str.Remove(0, 1);
            }

            for (var i = str.Length - 1; i >= 0; i--) {
                if (str[i] == '.') {
                    str = str.Remove(i, 1);

                    return IntParseFast(str) * factor * sign;
                }

                factor *= 0.1;
            }

            return IntParseFast(str) * sign;
        }

        public static int IntParseFast(string value) {
            // An optimized int parse method.
            var result = 0;
            foreach (var c in value) {
                if (!(c >= '0' && c <= '9')) return result; // error

                result = 10 * result + (c - 48);
            }
            return result;
        }
    }
}