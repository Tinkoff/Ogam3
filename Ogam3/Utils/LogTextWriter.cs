using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Ogam3.Utils {
    public class LogTextWriter : TextWriter {
        private readonly TextWriter _tw;

        public static void InitLogMode() {
            Console.SetOut(new LogTextWriter(Console.Out));
        }

        public LogTextWriter(TextWriter tw) {
            _tw = tw;
        }

        public override Encoding Encoding => _tw.Encoding;

        public static string GetHeader() {
            return
                $"{Environment.NewLine}---------- EVENT-{Process.GetCurrentProcess().ProcessName}[{Process.GetCurrentProcess().Id}]-{DateTime.Now:yyyyMMdd:HHmmss:fff} ----------{Environment.NewLine}";
        }

        void LogEvent(string msg) {
            ((Action)delegate () {
                var standardOutput = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding);
                standardOutput.AutoFlush = true;

                var header = GetHeader();

                StringCat(ref msg);

                var sb = new StringBuilder(msg.Length + header.Length + 2);

                sb.AppendLine(header);
                sb.AppendLine(msg);

                standardOutput.Write(sb.ToString());
            }).BeginInvoke(null, null);
        }

        void LogText(string msg) {
            ((Action)delegate () {
                var standardOutput = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding);
                standardOutput.AutoFlush = true;

                StringCat(ref msg);

                standardOutput.Write(msg);
            }).BeginInvoke(null, null);
        }

        private void StringCat(ref string str) {
            const int maxStringLength = 30000;
            if (str.Length > maxStringLength) {
                str = str.Substring(0, maxStringLength) + "...";
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