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
using System.Runtime.InteropServices;
using System.Text;

namespace Ogam3.Lsp {
    public static class BinFormater {

        private struct Codes {
            public const byte Open = 0x01;
            public const byte Close = 0x02;
            public const byte Dot = 0x03;
            public const byte Integer16_16 = 0x0a;
            public const byte Integer16_8 = 0x0b;
            public const byte Integer16_8_Negate = 0x0c;
            public const byte Integer16_0 = 0x0d;
            public const byte Integer32_32 = (byte)'I';
            public const byte Integer64 = (byte)'l';
            public const byte Byte = (byte)'b';
            public const byte Bool = (byte)'B';
            public const byte Charter8 = (byte)'c';
            public const byte Charter32 = (byte)'C';
            public const byte Float32 = (byte)'f';
            public const byte Float64 = (byte)'F';
            public const byte SymbolShort = (byte)'s';
            public const byte SymbolLong = (byte)'S';
            public const byte String = (byte)'t';
            public const byte StreamShort = (byte)'r';
            public const byte StreamLong = (byte)'R';
            public const byte Null = (byte)'n';
            public const byte DateTime = (byte)'d';
            public const byte SpecialMessage = (byte)'e';
        }

        public static bool IsPrimitive(Type t) {
            return
                t == typeof(ushort)
                || t == typeof(short)
                || t == typeof(uint)
                || t == typeof(int)
                || t == typeof(ulong)
                || t == typeof(long)
                || t == typeof(float)
                || t == typeof(double)
                || t == typeof(byte)
                || t == typeof(bool)
                || t == typeof(string)
                || t == typeof(MemoryStream)
                || t == typeof(DateTime)
                || t == typeof(SpecialMessage);
        }

        private static byte[] X1 = new byte[] {0x00};
        private static byte[] XN1 = new byte[] { 0xFF };

        public static Cons Read(MemoryStream data) {
            var stack = new Stack<Cons>();
            var root = new Cons();
            stack.Push(root);
            var isDot = false; // подумать

            var set = new Action<object>(o => {
                if (isDot) {
                    stack.Peek().SetCdr(o);
                    isDot = false;
                }
                else {
                    stack.Peek().Add(o);
                }
            });

            while (true) {
                var b = data.ReadByte();

                if (b <= 0) {return root;} // EOS

                switch (b) {
                    case Codes.Open: {
                        var nod = new Cons();
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                    case Codes.Close:
                        stack.Pop();
                        break;
                    case Codes.Dot:
                        isDot = true;
                        break;
                        //FIXED SIZE
                    case Codes.Integer16_16:
                        set(BitConverter.ToInt16(R(data, 2), 0));
                        break;
                    case Codes.Integer16_8:
                        set(BitConverter.ToInt16(R(data, 1, X1), 0));
                        break;
                    case Codes.Integer16_8_Negate:
                        set(BitConverter.ToInt16(R(data, 1, XN1), 0));
                        break;
                    case Codes.Integer16_0:
                        set((short)0);
                        break;
                    case Codes.Integer32_32:
                        set(BitConverter.ToInt32(R(data, 4), 0));
                        break;
                    case Codes.Integer64:
                        set(BitConverter.ToInt64(R(data, 8), 0));
                        break;
                    case Codes.Byte:
                        set(data.ReadByte());
                        break;
                    case Codes.Bool:
                        set(data.ReadByte() != 0);
                        break;
                    case Codes.Charter8:
                        set((char)data.ReadByte());
                        break;
                    case Codes.Charter32:
                        set(Encoding.UTF32.GetChars(R(data, 4)).FirstOrDefault());
                        break;
                    case Codes.Float32:
                        set(BitConverter.ToSingle(R(data, 4), 0));
                        break;
                    case Codes.Float64:
                        set(BitConverter.ToDouble(R(data, 8), 0));
                        break;
                        //FLOAT SIZE
                    case Codes.SymbolShort:
                        set(new Symbol(Encoding.UTF8.GetString(R(data, data.ReadByte()))));
                        break;
                    case Codes.SymbolLong:
                        set(new Symbol(Encoding.UTF8.GetString(R(data, BitConverter.ToInt16(R(data, 2), 0)))));
                        break;
                    case Codes.String:
                        set(Encoding.UTF8.GetString(R(data, BitConverter.ToInt32(R(data, 4), 0))));
                        break;
                    case Codes.StreamShort:
                        set(new MemoryStream(R(data, BitConverter.ToInt32(R(data, 4), 0))));
                        break;
                    case Codes.Null:
                        set(null);
                        break;
                    case Codes.DateTime:
                        set(DateTime.FromBinary(BitConverter.ToInt64(R(data, 8), 0)));
                        break;
                    case Codes.StreamLong: // TODO
                        throw new Exception("Not supported");
                    case Codes.SpecialMessage:
                        set(new SpecialMessage(Encoding.UTF8.GetString(R(data, BitConverter.ToInt32(R(data, 4), 0)))));
                        break;
                    case 'Q': {
                        var nod = new Cons(new Symbol("quote"));
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                    case 'V': {
                        var nod = new Cons(new Symbol("vector"));
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                        default:
                        throw new Exception("Bad data format!");
                }
            }
        }

        private static byte[] R(Stream data, int count, byte[] trailer = null) {
            byte[] buffer;
            if (trailer == null) {
                buffer = new byte[count];
            }
            else {
                buffer = new byte[count + trailer.Length];
                Array.Copy(trailer, 0, buffer, count, trailer.Length);
            }

            var res = data.Read(buffer, 0, count);

            return res != count ? null : buffer;
        }

        public static MemoryStream Write(object tree) {
            var ms = new MemoryStream();

            WriteItem(ms, tree);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        private static MemoryStream WriteConsSeq(MemoryStream ms, Cons tree) {
            ms.WriteByte(Codes.Open);

            foreach (var o in tree.GetIterator()) {
                WriteItem(ms, o.Car());

                var cdr = o.Cdr();

                if (cdr is Cons || (cdr == null)) continue;

                ms.WriteByte(Codes.Dot);
                WriteItem(ms, cdr);
            }

            ms.WriteByte(Codes.Close);

            return ms;
        }

        private static MemoryStream WriteItem(MemoryStream ms, object item) {
            var writeCode = new Action<byte>(ms.WriteByte);

            if (item is Cons) {
                WriteConsSeq(ms, item as Cons);
            } else if (item is int) {
                writeCode(Codes.Integer32_32);
                MsWrite(ms, BitConverter.GetBytes((int)item));
            } else if (item is uint) {
                writeCode(Codes.Integer32_32);
                MsWrite(ms, BitConverter.GetBytes((uint)item));
            } else if (item is long) {
                writeCode(Codes.Integer64);
                MsWrite(ms, BitConverter.GetBytes((long)item));
            } else if (item is ulong) {
                writeCode(Codes.Integer64);
                MsWrite(ms, BitConverter.GetBytes((ulong)item));
            } else if (item is short) {
                var val = (short) item;
                if ((val <= 255) && (val >= -255)) {
                    writeCode(val > 0 ? Codes.Integer16_8 : Codes.Integer16_8_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                }
                else {
                    writeCode(Codes.Integer16_16);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is ushort) {
                writeCode(Codes.Integer16_16);
                MsWrite(ms, BitConverter.GetBytes((ushort)item));
            } else if (item is float) {
                writeCode(Codes.Float32);
                MsWrite(ms, BitConverter.GetBytes((float)item));
            } else if (item is double) {
                writeCode(Codes.Float64);
                MsWrite(ms, BitConverter.GetBytes((double)item));
            } else if (item is byte) {
                writeCode(Codes.Byte);
                ms.WriteByte((byte)item);
            } else if (item is bool) {
                writeCode(Codes.Bool);
                ms.WriteByte((byte)((bool)item ? 1 : 0));
            } else if (item == null) {
                writeCode(Codes.Null);
            } else if (item is DateTime) {
                writeCode(Codes.DateTime);
                MsWrite(ms, BitConverter.GetBytes(((DateTime)item).ToBinary()));
            }else if (item is char) {
                if ((uint) item <= 255) {
                    writeCode(Codes.Charter8);
                    ms.WriteByte((byte)item);
                }
                else {
                    writeCode(Codes.Charter32);
                    MsWrite(ms, Encoding.UTF32.GetBytes(new []{(char) item}));
                }
            } else if (item is Symbol) {
                var bytes = Encoding.UTF8.GetBytes((item as Symbol).Name);

                if (bytes.Length <= 255) {
                    writeCode(Codes.SymbolShort);
                    ms.WriteByte((byte)bytes.Length);
                }
                else {
                    writeCode(Codes.SymbolLong);
                    MsWrite(ms, BitConverter.GetBytes((short)bytes.Length));
                }

                MsWrite(ms, bytes);
            } else if (item is string) {
                var bytes = Encoding.UTF8.GetBytes((item as string));
                writeCode(Codes.String);
                MsWrite(ms, BitConverter.GetBytes((int)bytes.Length));
                MsWrite(ms, bytes);
            } else if (item is MemoryStream) {
                var bytes = ReadFully(item as Stream);
                writeCode(Codes.StreamShort);
                MsWrite(ms, BitConverter.GetBytes((uint)bytes.Length));
                MsWrite(ms, bytes);
            }  else if (item is SpecialMessage) {
                var bytes = Encoding.UTF8.GetBytes((item as SpecialMessage).Message);
                writeCode(Codes.SpecialMessage);
                MsWrite(ms, BitConverter.GetBytes((int)bytes.Length));
                MsWrite(ms, bytes);
            } else {
                throw new Exception($"The {item.GetType()} is unknown datatype");
            }

            return ms;
        }

        public static byte[] ReadFully(Stream input) {
            if (input is MemoryStream) {
                return (input as MemoryStream).ToArray();
            }

            using (var ms = new MemoryStream()) {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static MemoryStream MsWrite(MemoryStream ms, byte[] bytes) {
            ms.Write(bytes, 0, bytes.Length);
            return ms;
        }

        private static byte[] Foo<T>(this T input) where T : struct {
            int size = Marshal.SizeOf(typeof(T));
            var result = new byte[size];
            var gcHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            Marshal.Copy(gcHandle.AddrOfPinnedObject(), result, 0, size);
            gcHandle.Free();
            return result;
        }
    }
}
