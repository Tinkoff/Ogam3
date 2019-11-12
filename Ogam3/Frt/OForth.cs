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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;


namespace Ogam3.Frt {
    /// <summary>
    /// Simple forth interpreter
    /// </summary>
    public class OForth {
        public object[] Mem;     // Programm memory
        public Stack<int> RS;    // Return stack
        public Stack<object> DS; // Data stack
        public int IP;           // Interpreter pointer
        public int WP;           // Word or work pointer

        public delegate void CoreCall();
        public List<CoreCall> Core; // Base instructions

        public TextReader Input;
        public TextWriter Output;

        private int _hereP; // Here pointer
        public int Here {   // Here variable
            get => (int)Mem[_hereP];
            set => Mem[_hereP] = value;
        }

        public Dictionary<string, List<WordHeader>> Entries; // Word header dictionary
        public WordHeader LastWordHeader;
        public class WordHeader {
            public int Address;
            public bool Immediate;
            public bool IsEnable;
        }

        public OForth(int memorySize = 1024, TextWriter output = null) {
            Mem = new object[memorySize];
            RS = new Stack<int>();
            DS = new Stack<object>();
            Entries = new Dictionary<string, List<WordHeader>>();

            // Set default IO
            Input = Console.In;
            Output = output ?? Console.Out;

            // Initialise base system
            InitCore();
            InitExtra();
        }

        #region Initialisation
        private void InitCore() {
            Core = new List<CoreCall>();
            var here = 0;
            void SetCoreWord(string word, CoreCall handler, bool immediate = false) {
                var address = Core.Count;
                Core.Add(handler);                // set core handler
                Define(word, address, immediate); // set word entry
                Mem[address] = address;           // set core address
                here++;                           // increase here
            }

            // Core
            SetCoreWord("nop", Nop);
            SetCoreWord("next", Next);
            SetCoreWord("doList", DoList);
            SetCoreWord("exit", Exit);
            SetCoreWord("execute", Execute);
            SetCoreWord("doLit", DoLit);
            SetCoreWord(":", BeginDefWord);
            SetCoreWord(";", EndDefWord, true);
            SetCoreWord("branch", Branch);
            SetCoreWord("0branch", ZBranch);
            SetCoreWord("here", GetHereAddr);
            SetCoreWord("quit", Quit);
            SetCoreWord("dump", Dump);
            SetCoreWord("words", Words);
            SetCoreWord("'", Tick);
            SetCoreWord(",", Comma);
            SetCoreWord("[", Lbrac, true);
            SetCoreWord("]", Rbrac);
            SetCoreWord("immediate", Immediate, true);
            // Mem
            SetCoreWord("!", WriteMem);
            SetCoreWord("@", ReadMem);
            SetCoreWord("variable", Variable);
            SetCoreWord("constant", Constant);
            // RW
            SetCoreWord(".", Dot);
            SetCoreWord(".s", DotS);
            SetCoreWord("cr", Cr);
            SetCoreWord("bl", Bl);
            SetCoreWord("word", ReadWord, true);
            SetCoreWord("s\"", ReadString, true);
            SetCoreWord("key", Key);
            // Comment
            SetCoreWord("(", Comment, true);
            SetCoreWord("\\", CommentLine, true);
            // .net mem
            SetCoreWord("null", Null);
            SetCoreWord("new", New);
            SetCoreWord("type", GetType);
            SetCoreWord("m!", SetMember);
            SetCoreWord("m@", GetMember);
            SetCoreWord("ms!", SetStaticMember);
            SetCoreWord("ms@", GetStaticMember);
            SetCoreWord("load-assembly", LoadAssembly);
            SetCoreWord("invk", invk);
            // Boolean
            SetCoreWord("true", True);
            SetCoreWord("false", False);
            SetCoreWord("and", And);
            SetCoreWord("or", Or);
            SetCoreWord("xor", Xor);
            SetCoreWord("not", Not);
            SetCoreWord("invert", Invert);
            SetCoreWord("=", Eql);
            SetCoreWord("<>", NotEql);
            SetCoreWord("<", Less);
            SetCoreWord(">", Greater);
            SetCoreWord("<=", LessEql);
            SetCoreWord(">=", GreaterEql);
            // Math
            SetCoreWord("-", Minus);
            SetCoreWord("+", Plus);
            SetCoreWord("*", Multiply);
            SetCoreWord("/", Devide);
            SetCoreWord("mod", Mod);
            SetCoreWord("1+", Inc);
            SetCoreWord("1-", Dec);
            // Stack
            SetCoreWord("drop", Drop);
            SetCoreWord("swap", Swap);
            SetCoreWord("dup", Dup);
            SetCoreWord("over", Over);
            SetCoreWord("rot", Rot);
            SetCoreWord("nrot", Nrot);

            _hereP = here;
            Here = ++here;
        }

        void InitExtra() {
            // Print variable
            Eval(": ? @ . ;");

            // Allocate n bytes
            Eval(": allot here @ + here ! ;");

            // control flow
            Eval(": if immediate doLit [ ' 0branch , ] , here @ 0 , ;");
            Eval(": then immediate dup here @ swap - swap ! ;");
            Eval(": else immediate [ ' branch , ] , here @ 0 , swap dup here @ swap - swap ! ;");

            // loops
            Eval(": begin immediate here @ ;");
            Eval(": until immediate doLit [ ' 0branch , ] , here @ - , ;");
            Eval(": again immediate doLit [ ' branch , ] , here @ - , ;");
            Eval(": while immediate doLit [ ' 0branch , ] , here @ 0 , ;");
            Eval(": repeat immediate doLit [ ' branch , ] , swap here @ - , dup here @ swap - swap ! ;");

            // C like comment
            Eval(": // immediate [ ' \\ , ] ;");
        }
        #endregion
        #region Implementation of base operations
        private void Nop() { }

        private void Next() {
            while (true) {
                if (IP == 0)
                    return;
                WP = (int)Mem[IP++];
                Core[(int)Mem[WP]]();
            }
        }

        private void DoList() {
            RS.Push(IP);
            IP = WP + 1;
        }

        private void Exit() {
            IP = RS.Pop();
        }

        private void Execute() {
            var address = (int)DS.Pop();
            if (address < Core.Count) { // eval core
                Core[address]();        // invoke core function
            } else {                    // eval word
                //IP == 4;              // core word execute
                WP = address;           // set eval address
                DoList();               // fake doList
                Next();                 // run evaluator
            }
        }

        private void DoLit() {
            DS.Push(Mem[IP++]);
        }

        private void BeginDefWord() {
            AddHeader(ReadWord(Input));
            AddOp(LookUp("doList").Address);
            IsEvalMode = false;
            LastWordHeader.IsEnable = false;
        }

        private void EndDefWord() {
            AddOp(LookUp("exit").Address);
            IsEvalMode = true;
            LastWordHeader.IsEnable = true;
        }

        private void Branch() {
            IP += ((int)Mem[IP]);
        }

        private void ZBranch() {
            if ((bool)DS.Pop() == false) {
                IP += ((int)Mem[IP]);
            } else {
                IP++;
            }
        }

        private void GetHereAddr() {
            DS.Push(_hereP);
        }

        private void Quit() {
            Environment.Exit(0);
        }

        private void Dump() {
            Output.WriteLine("");
            Output.WriteLine("-----  MEMORY DUMP  -----");
            for (var i = 0; i < Here + 3; i++) {
                var name = "???";
                if (Mem[i] is int) {
                    var ka = SearchKnowAddress((int)Mem[i]);
                    var sEntry = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == ka));
                    name = sEntry.Key;
                }

                var entryWord = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == i));

                if (!string.IsNullOrWhiteSpace(entryWord.Key)) {
                    Output.WriteLine("");
                    Output.WriteLine($"word ==>> '{entryWord.Key}'");
                }

                Output.WriteLine($"[{i:00000}] -> <{(Mem[i] == null ? "???" : Mem[i].GetType().Name)}> : {Mem[i]:00000} : \"{name}\"");
            }
        }

        private void Words() {
            Output.WriteLine("");
            foreach (var entry in Entries) {
                Output.Write($"{entry.Key} ");
            }
            Output.WriteLine("");
        }

        private void Tick() {
            var word = ReadWord(Input);
            var address = LookUp(word).Address;
            DS.Push(address);
        }

        private void Comma() {
            AddOp(DS.Pop());
        }

        private void Lbrac() {
            IsEvalMode = true;
        }

        private void Rbrac() {
            IsEvalMode = false;
        }

        private void Immediate() {
            LastWordHeader.Immediate = true;
        }

        private void WriteMem() {
            var address = (int)DS.Pop();
            var data = DS.Pop();
            Mem[address] = data;
        }

        private void ReadMem() {
            var address = (int)DS.Pop();
            DS.Push(Mem[address]);
        }

        private void Variable() {
            var here = Here++;
            AddHeader(ReadWord(Input));
            AddOp(LookUp("doList").Address);
            AddOp(LookUp("doLit").Address);
            AddOp(here);
            AddOp(LookUp("exit").Address);
        }

        private void Constant() {
            AddHeader(ReadWord(Input));
            AddOp(LookUp("doList").Address);
            AddOp(LookUp("doLit").Address);
            AddOp(DS.Pop());
            AddOp(LookUp("exit").Address);
        }

        private void Dot() {
            Output.Write(DS.Pop());
        }

        private void DotS() {
            foreach (var o in DS) {
                if (o != null) {
                    Output.WriteLine(o);
                } else {
                    Output.WriteLine("null");
                }
            }
        }

        private void Cr() {
            Output.WriteLine("");
        }

        private void Bl() {
            Output.Write(" ");
        }

        private void ReadWord() {
            var str = ReadWord(Input);
            if (IsEvalMode) {
                DS.Push(str);
            } else {
                AddOp(LookUp("doLit").Address);
                AddOp(str);
            }
        }

        private void ReadString() {
            var str = ReadString(Input);
            if (IsEvalMode) {
                DS.Push(str);
            } else {
                AddOp(LookUp("doLit").Address);
                AddOp(str);
            }
        }

        private void Key() {
            DS.Push(Input.Read());
        }

        private void Comment() {
            SkipComment(Input, new[] { ')' });
        }

        private void CommentLine() {
            SkipComment(Input, new[] { '\n', '\r' });
        }

        private void Null() {
            DS.Push(null);
        }

        private void New() {
            var type = DS.Pop() as Type;

            if (type == null) {
                throw new Exception($"Undefined type.");
            }

            //var ctors = type.GetConstructors(); TODO search ctor

            var ctors = type.GetConstructors().First();

            var cArg = new List<object>();

            foreach (var pi in ctors.GetParameters()) {
                if (DS.Any()) {
                    cArg.Add(DS.Pop());
                } else {
                    var sb = new StringBuilder();
                    sb.AppendLine($":: Error constructor call \"{ctors.Name}\"");
                    sb.AppendLine($":: Arity mismatch {ctors}");
                    sb.AppendLine($":: Expected {ctors.GetParameters().Length} arguments");
                    sb.AppendLine($":: Given {cArg.Count} arguments");
                    throw new Exception(sb.ToString());
                }
            }

            DS.Push(Activator.CreateInstance(type, cArg.ToArray()));
        }

        private void GetType() {
            var fullyQualifiedName = DS.Pop() as string;
            if (string.IsNullOrWhiteSpace(fullyQualifiedName)) {
                throw new Exception("Undefined name of type.");
            }

            var type = Utils.Reflect.TryFindType(fullyQualifiedName);
            if (type == null) {
                throw new Exception($"The '{fullyQualifiedName}' class not found.");
            }

            DS.Push(type);
        }

        private void SetMember() {
            var inst = DS.Pop();
            if (inst == null) {
                throw new Exception("Instance is null");
            }

            var memberName = DS.Pop() as string;
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new Exception("Undefined name of member.");
            }

            var value = DS.Pop();

            if (!Utils.Reflect.SetPropValue(inst, memberName, value)) {
                throw new Exception("Value not set");
            }
        }

        private void GetMember() {
            var inst = DS.Pop();
            if (inst == null) {
                throw new Exception("Instance is null");
            }

            var memberName = DS.Pop() as string;
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new Exception("Undefined name of member.");
            }

            DS.Push(Utils.Reflect.GetPropValue(inst, memberName));
        }

        private void SetStaticMember() {
            var type = DS.Pop() as Type;
            if (type == null) {
                throw new Exception("Is not Type of class");
            }

            var memberName = DS.Pop() as string;
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new Exception("Undefined name of member.");
            }

            var value = DS.Pop();

            if (!Utils.Reflect.SetStaticPropValue(type, memberName, value)) {
                throw new Exception("Value not set");
            }
        }

        private void GetStaticMember() {
            var type = DS.Pop() as Type;
            if (type == null) {
                throw new Exception("Is not Type of class");
            }

            var memberName = DS.Pop() as string;
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new Exception("Undefined name of member.");
            }

            DS.Push(Utils.Reflect.GetStaticPropValue(type, memberName));
        }

        private void LoadAssembly() {
            var path = DS.Pop() as string;

            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception("Path is empty");
            }

            if (!File.Exists(path)) {
                throw new Exception("Assembly not found");
            }

            Assembly.LoadFrom(path);
        }

        private void invk() {
            var func = DS.Pop() as MulticastDelegate;
            if (func == null) {
                throw new Exception("Is not callable");
            }

            var cArg = new List<object>();

            foreach (var pi in func.Method.GetParameters()) {
                if (DS.Any()) {
                    cArg.Add(DS.Pop());
                } else {
                    var sb = new StringBuilder();
                    sb.AppendLine($":: Error function call \"{func.Method.Name}\"");
                    sb.AppendLine($":: Arity mismatch {func}");
                    sb.AppendLine($":: Expected {func.Method.GetParameters().Length} arguments");
                    sb.AppendLine($":: Given {cArg.Count} arguments");
                    throw new Exception(sb.ToString());
                }
            }

            var res = func.DynamicInvoke(cArg.ToArray());

            if (func.Method.ReturnType != typeof(void)) {
                DS.Push(res);
            }
        }

        private void True() {
            DS.Push(true);
        }

        private void False() {
            DS.Push(false);
        }

        private void And() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b & a);
        }

        private void Or() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b | a);
        }

        private void Xor() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b ^ a);
        }

        private void Not() {
            var a = DS.Pop() as dynamic;
            DS.Push(!a);
        }

        private void Invert() {
            var a = DS.Pop() as dynamic;
            DS.Push(~a);
        }

        private void Eql() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b == a);
        }

        private void NotEql() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b != a);
        }

        private void Less() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b < a);
        }

        private void Greater() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b > a);
        }

        private void LessEql() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b <= a);
        }

        private void GreaterEql() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b >= a);
        }

        private void Minus() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b - a);
        }

        private void Plus() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b + a);
        }

        private void Multiply() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b * a);
        }

        private void Devide() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b / a);
        }

        private void Mod() {
            var a = DS.Pop() as dynamic;
            var b = DS.Pop() as dynamic;
            DS.Push(b % a);
        }

        private void Inc() {
            var a = DS.Pop() as dynamic;
            DS.Push(a + 1);
        }

        private void Dec() {
            var a = DS.Pop() as dynamic;
            DS.Push(a - 1);
        }

        private void Drop() {
            DS.Pop();
        }

        private void Swap() {
            var a = DS.Pop();
            var b = DS.Pop();
            DS.Push(a);
            DS.Push(b);
        }

        private void Dup() {
            DS.Push(DS.Peek());
        }

        private void Over() {
            DS.Push(DS.ElementAt(1));
        }

        private void Rot() {
            var a = DS.Pop();
            var b = DS.Pop();
            var c = DS.Pop();
            DS.Push(b);
            DS.Push(a);
            DS.Push(c);
        }

        private void Nrot() {
            var a = DS.Pop();
            var b = DS.Pop();
            var c = DS.Pop();
            DS.Push(a);
            DS.Push(c);
            DS.Push(b);
        }
        #endregion
        #region Helpers
        private WordHeader LookUp(string word) {
            if (Entries.ContainsKey(word)) {
                return Entries[word].Last(e => e.IsEnable);
            }

            return null;
        }

        private void Define(string word, int entry, bool immediate = false) {
            List<WordHeader> set = null;
            if (Entries.ContainsKey(word)) {
                set = Entries[word];
            } else {
                set = new List<WordHeader>();
                Entries[word] = set;
            }

            LastWordHeader = new WordHeader() { Address = entry, Immediate = immediate, IsEnable = true};
            set.Add(LastWordHeader);
        }

        private void AddHeader(string word) {
            Define(word, Here);
        }

        private void AddOp(object op) {
            Mem[Here++] = op;
        }

        public void DefineWord(string word, params string[] subWords) {
            AddHeader(word);
            AddOp(LookUp("doList").Address);
            foreach (var w in subWords) {
                var lookup = LookUp(w);
                if (lookup != null) {
                    AddOp(LookUp(w).Address);
                } else if (IsConstant(w)) {
                    AddOp(LookUp("doLit").Address);
                    AddOp(ParseNumber(w));
                } else {
                    Output.WriteLine($"Unknown word {w}");
                }
            }
            AddOp(LookUp("exit").Address);
        }

        public void Callback(string word, MulticastDelegate action) {
            if (string.IsNullOrWhiteSpace(word) || word.Any(c => " \n\r\t".Any(cw => cw == c))) {
                throw new Exception("invalid format of word");
            }

            DS.Push(action);
            Eval($": {word} [ ' doLit , , ] invk ;");
        }

        #endregion
        #region Text interpreter
        static bool IsConstant(string word) {
            return IsDigit(word[0]) || (word.Length >= 2 && (word[0] == '+' || word[0] == '-') && IsDigit(word[1]));
        }

        static bool IsDigit(char c) {
            return (c >= '0' && c <= '9');
        }

        // An optimized int parse method.
        static int IntParseFast(string value) {
            var result = 0;
            foreach (var c in value) {
                if (!(c >= '0' && c <= '9'))
                    return result; // error

                result = 10 * result + (c - 48);
            }
            return result;
        }

        static object ParseNumber(string str) {
            var factor = 1.0;
            var sign = 1;
            if (str[0] == '-') {
                sign = -1;
                str = str.Remove(0, 1);
            } else if (str[0] == '+') {
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

        public void Execute(int address) {
            try {
                if (address < Core.Count) { // eval core
                    Core[address]();        // invoke core function
                } else {                    // eval word
                    IP = 0;                 // set return address
                    WP = address;           // set eval address
                    DoList();               // fake doList
                    Next();                 // run evaluator
                }
            } catch (Exception e) {
                Output.WriteLine(e.Message);
                var wpEntry = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == WP));
                var ipEntry = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == SearchKnowAddress(IP)));
                Output.WriteLine($"WP = {WP:00000} - '{wpEntry.Key}', IP = {IP:00000} - '{ipEntry.Key}'");

                if (RS.Any()) {
                    Output.WriteLine("Stack trace...");
                    foreach (var a in RS) {
                        var ka = SearchKnowAddress(a);
                        var sEntry = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == ka));
                        Output.WriteLine($"...{a:00000} -- {sEntry.Key}");
                    }
                    RS.Clear();
                    DS.Clear();
                } else if (address < Core.Count) {
                    var entry = Entries.FirstOrDefault(d => d.Value.Any(en => en.Address == address));
                    Output.WriteLine($"Core word is {entry.Key}");
                }

                IP = WP = 0;
            }
        }

        public int SearchKnowAddress(int address) {
            var knownAddresses = Entries.SelectMany(d => d.Value).Select(en => en.Address).OrderBy(a => a).ToArray();
            var ka = 0;
            foreach (var knownAddress in knownAddresses) {
                if (address >= knownAddress) {
                    ka = knownAddress;
                } else {
                    break;
                }
            }

            return ka;
        }

        public void Execute(string word) {
            var address = LookUp(word);
            if (address != null) {
                Execute(address.Address);
            } else {
                DS.Clear();
                Output.WriteLine($"The word {word} is undefined");
            }
        }


        private bool IsEvalMode = true;

        public void Eval(string str) {
            Eval(new StringReader(str));
        }

        public void Eval(TextReader textReader) {
            Input = textReader;
            Interpreter();
        }

        void Interpreter() {
            while (true) {
                var word = ReadWord(Input);

                if (string.IsNullOrWhiteSpace(word))
                    return; // EOF

                var lookup = LookUp(word);

                if (IsEvalMode) {
                    if (lookup != null) {
                        Execute(lookup.Address);
                    } else if (IsConstant(word)) {
                        DS.Push(ParseNumber(word));
                    } else {
                        DS.Clear();
                        Output.WriteLine($"The word {word} is undefined");
                    }
                } else { // program mode
                    if (lookup != null) {
                        if (lookup.Immediate) {
                            Execute(lookup.Address);
                        } else {
                            AddOp(lookup.Address);
                        }
                    } else if (IsConstant(word)) {
                        AddOp(LookUp("doLit").Address);
                        AddOp(ParseNumber(word));

                    } else {
                        IsEvalMode = true;
                        DS.Clear();
                        Output.WriteLine($"The word {word} is undefined");
                    }
                }
            }
        }

        static string ReadWord(TextReader sr) {
            var sb = new StringBuilder();
            var code = sr.Read();

            while (IsWhite((char)code) && code > 0) {
                code = sr.Read();
            }

            while (!IsWhite((char)code) && code > 0) {
                sb.Append((char)code);
                code = sr.Read();
            }

            return sb.ToString();
        }

        static string ReadString(TextReader sr) {
            var sb = new StringBuilder();
            var c = (char)sr.Read();

            while (IsWhite(c)) {
                c = (char)sr.Read();
            }

            while (c != '"') {
                sb.Append(c);
                c = (char)sr.Read();
            }

            return sb.ToString();
        }

        static void SkipComment(TextReader sr, char[] stop) {
            var c = (char)sr.Read();

            while (stop.All(s => s != c)) {
                c = (char)sr.Read();
            }

        }

        static bool IsWhite(char c) {
            return " \n\r\t".Any(ch => ch == c);
        }
        #endregion
    }
}
