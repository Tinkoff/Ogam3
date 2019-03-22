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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ogam3.Utils {
    public class LogTextWriter : TextWriter {
        private object _locker;
        private readonly TextWriter _tw;
        private static int _maxStringLength;
        private StreamWriter _sw;
        private Queue<string> MessageQueue;

        public StreamWriter Sw =>
            _sw ?? (_sw = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });

        public static void InitLogMode(int maxStringLength = 30000) {
            _maxStringLength = maxStringLength;
            Console.SetOut(new LogTextWriter(Console.Out));
        }

        public LogTextWriter(TextWriter tw) {
            _tw = tw;
            _locker = new object();
            MessageQueue = new Queue<string>();
            new Thread(() => {
                while (true) {
                    try {
                        if (MessageQueue.Any()) {
                            Queue<string> writeQueue;
                            lock (_locker) {
                                writeQueue = new Queue<string>(MessageQueue);
                                MessageQueue.Clear();
                            }

                            while (writeQueue.Any()) {
                                Sw?.Write(writeQueue.Dequeue());
                            }
                            Sw?.Flush();
                        } else {
                            Thread.Sleep(1000);
                        }
                    } catch (Exception e) {
                        Sw?.WriteLine(e);
                    }
                }
            }) { IsBackground = true }.Start();
        }

        public override Encoding Encoding => _tw.Encoding;

        public static string GetHeader() {
            return
                $"{Environment.NewLine}---------- EVENT-{Process.GetCurrentProcess().ProcessName}[{Process.GetCurrentProcess().Id}]-{DateTime.Now:yyyyMMdd:HHmmss:fff} ----------{Environment.NewLine}";
        }

        void TrimLog() {
            if (MessageQueue.Count > 500000) {
                while (MessageQueue.Count > 300000) {
                    MessageQueue.Dequeue();
                }

                MessageQueue.Enqueue($"### LOG WAS TRIMMED...{Environment.NewLine}");
            }
        }

        void LogEvent(string msg) {
            if (msg == null) {
                msg = string.Empty;
            }

            StringCat(ref msg);
            var header = GetHeader();
            lock (_locker) {
                var sb = new StringBuilder(header.Length + msg.Length);
                sb.AppendLine(header);
                sb.AppendLine(msg);
                MessageQueue.Enqueue(sb.ToString());
                TrimLog();
            }
        }

        void LogText(string msg) {
            StringCat(ref msg);
            lock (_locker) {
                MessageQueue.Enqueue(msg);
                TrimLog();
            }
        }

        private void StringCat(ref string str) {
            if (str?.Length > _maxStringLength) {
                str = str.Substring(0, _maxStringLength) + "...";
            }
        }

        public override void WriteLine(string s) {
            LogEvent(s);
        }

        public override void WriteLine(string s, object obj0) {
            LogEvent(string.Format(s, obj0));
        }

        public override void WriteLine(string s, object obj0, object obj1) {
            LogEvent(string.Format(s, obj0, obj1));
        }

        public override void WriteLine(string s, object obj0, object obj1, object obj2) {
            LogEvent(string.Format(s, obj0, obj1, obj2));
        }

        public override void WriteLine(string s, params object[] obj) {
            LogEvent(string.Format(s, obj));
        }

        public override void WriteLine(char c) {
            LogEvent(c.ToString());
        }

        public override void WriteLine(char[] buffer) {
            LogEvent(buffer.ToString());
        }

        public override void WriteLine(bool b) {
            LogEvent(b.ToString());
        }

        public override void WriteLine(int i) {
            LogEvent(i.ToString());
        }

        public override void WriteLine(uint i) {
            LogEvent(i.ToString());
        }

        public override void WriteLine(long l) {
            LogEvent(l.ToString());
        }

        public override void WriteLine(ulong l) {
            LogEvent(l.ToString());
        }

        public override void WriteLine(float f) {
            LogEvent(f.ToString());
        }

        public override void WriteLine(double d) {
            LogEvent(d.ToString());
        }

        public override void WriteLine(decimal dc) {
            LogEvent(dc.ToString());
        }

        public override void WriteLine(object o) {
            LogEvent(o.ToString());
        }

        //*********************************************

        public override void Write(long value) {
            LogText(value.ToString());
        }

        public override void Write(int value) {
            LogText(value.ToString());
        }

        public override void Write(ulong value) {
            LogText(value.ToString());
        }

        public override void Write(float value) {
            LogText(value.ToString());
        }

        public override void Write(uint value) {
            LogText(value.ToString());
        }

        public override void Write(double value) {
            LogText(value.ToString());
        }

        public override void Write(char[] buffer) {
            LogText(buffer.ToString());
        }

        public override void Write(char value) {
            LogText(value.ToString());
        }

        public override void Write(bool value) {
            LogText(value.ToString());
        }

        public override void Write(object value) {
            LogText(value.ToString());
        }

        public override void Write(decimal value) {
            LogText(value.ToString());
        }

        public override void Write(string value) {
            LogText(string.Format(value));
        }

        public override void Write(string format, object arg0) {
            LogText(string.Format(format, arg0));
        }

        public override void Write(string format, params object[] arg) {
            LogText(string.Format(format, arg));
        }

        public override void Write(char[] buffer, int index, int count) {
            throw new Exception("UNHANDLED");
        }

        public override void Write(string format, object arg0, object arg1) {
            LogText(string.Format(format, arg0, arg1));
        }

        public override void Write(string format, object arg0, object arg1, object arg2) {
            LogText(string.Format(format, arg0, arg1, arg2));
        }
    }
}