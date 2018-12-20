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
using System.IO;
using System.Text;

namespace Ogam3.Utils {
    public class LogTextWriter : TextWriter {
        private object _locker;

        private readonly TextWriter _tw;

        private static int _maxStringLength;

        private StreamWriter _sw;

        public StreamWriter Sw =>
            _sw ?? (_sw = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) {AutoFlush = true});

        public static void InitLogMode(int maxStringLength=30000) {
            _maxStringLength = maxStringLength;
            Console.SetOut(new LogTextWriter(Console.Out));
        }

        public LogTextWriter(TextWriter tw) {
            _tw = tw;
            _locker = new object();
        }

        public override Encoding Encoding => _tw.Encoding;

        public static string GetHeader() {
            return
                $"{Environment.NewLine}---------- EVENT-{Process.GetCurrentProcess().ProcessName}[{Process.GetCurrentProcess().Id}]-{DateTime.Now:yyyyMMdd:HHmmss:fff} ----------{Environment.NewLine}";
        }

        void LogEvent(string msg) {
            StringCat(ref msg);
            var header = GetHeader();
            lock (_locker) {
                Sw.WriteLine(header);
                Sw.WriteLine(msg);
                Sw.Flush();
            }
        }

        void LogText(string msg) {
            StringCat(ref msg);
            lock (_locker) {
                Sw.Write(msg);
                Sw.Flush();
            }
        }

        private void StringCat(ref string str) {
            if (str.Length > _maxStringLength) {
                str = str.Substring(0, _maxStringLength) + "...";
            }
        }

        public override void WriteLine(string s) {
            ((Action)delegate () {
                LogEvent(s);
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(string s, object obj0) {
            ((Action)delegate () {
                LogEvent(string.Format(s, obj0));
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(string s, object obj0, object obj1) {
            ((Action)delegate () {
                LogEvent(string.Format(s, obj0, obj1));
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(string s, object obj0, object obj1, object obj2) {
            ((Action)delegate () {
                LogEvent(string.Format(s, obj0, obj1, obj2));
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(string s, params object[] obj) {
            ((Action)delegate () {
                LogEvent(string.Format(s, obj));
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(char c) {
            ((Action)delegate () {
                LogEvent(c.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(char[] buffer) {
            ((Action)delegate () {
                LogEvent(buffer.ToString());
        }).BeginInvoke(null, null);
        }

        public override void WriteLine(bool b) {
            ((Action)delegate () {
                LogEvent(b.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(int i) {
            ((Action)delegate () {
                LogEvent(i.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(uint i) {
            ((Action)delegate () {
                LogEvent(i.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(long l) {
            ((Action)delegate () {
                LogEvent(l.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(ulong l) {
            ((Action)delegate () {
                LogEvent(l.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(float f) {
            ((Action)delegate () {
                LogEvent(f.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(double d) {
            ((Action)delegate () {
                LogEvent(d.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(decimal dc) {
            ((Action)delegate () {
                LogEvent(dc.ToString());
            }).BeginInvoke(null, null);
        }

        public override void WriteLine(object o) {
            ((Action)delegate () {
                LogEvent(o.ToString());
            }).BeginInvoke(null, null);
        }

        //*********************************************

        public override void Write(long value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(int value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(ulong value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(float value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(uint value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(double value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(char[] buffer) {
            ((Action)delegate () {
                LogText(buffer.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(char value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }
        
        public override void Write(bool value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(object value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }

        public override void Write(decimal value) {
            ((Action)delegate () {
                LogText(value.ToString());
            }).BeginInvoke(null, null);
        }
        
        public override void Write(string value) {
            ((Action)delegate () {
                LogText(string.Format(value));
            }).BeginInvoke(null, null);
        }

        public override void Write(string format, object arg0) {
            ((Action)delegate () {
                LogText(string.Format(format, arg0));
            }).BeginInvoke(null, null);
        }

        public override void Write(string format, params object[] arg) {
            ((Action)delegate () {
                LogText(string.Format(format, arg));
            }).BeginInvoke(null, null);
        }

        public override void Write(char[] buffer, int index, int count) {
            throw new Exception("UNHANDLED");
        }

        public override void Write(string format, object arg0, object arg1) {
            ((Action)delegate () {
                LogText(string.Format(format, arg0, arg1));
            }).BeginInvoke(null, null);
        }

        public override void Write(string format, object arg0, object arg1, object arg2) {
            ((Action)delegate () {
                LogText(string.Format(format, arg0, arg1, arg2));
            }).BeginInvoke(null, null);
        }
    }
}