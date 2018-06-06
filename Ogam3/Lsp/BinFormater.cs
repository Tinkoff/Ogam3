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

            public const byte Integer16_16_u = 0x0e;
            public const byte Integer16_8_u = 0x0f;
            public const byte Integer16_0_u = 0x10;

            public const byte Integer32_32 = 0x11;
            public const byte Integer32_24 = 0x12;
            public const byte Integer32_24_Negate = 0x13;
            public const byte Integer32_16 = 0x14;
            public const byte Integer32_16_Negate = 0x15;
            public const byte Integer32_8 = 0x16;
            public const byte Integer32_8_Negate = 0x17;
            public const byte Integer32_0 = 0x18;

            public const byte Integer32_32_u = 0x19;
            public const byte Integer32_24_u = 0x1a;
            public const byte Integer32_16_u = 0x1b;
            public const byte Integer32_8_u = 0x1c;
            public const byte Integer32_0_u = 0x1d;

            public const byte Integer64_64 = 0x1e;
            public const byte Integer64_56 = 0x1f;
            public const byte Integer64_56_Negate = 0x20;
            public const byte Integer64_48 = 0x21;
            public const byte Integer64_48_Negate = 0x22;
            public const byte Integer64_40 = 0x23;
            public const byte Integer64_40_Negate = 0x24;
            public const byte Integer64_32 = 0x25;
            public const byte Integer64_32_Negate = 0x26;
            public const byte Integer64_24 = 0x27;
            public const byte Integer64_24_Negate = 0x28;
            public const byte Integer64_16 = 0x29;
            public const byte Integer64_16_Negate = 0x2a;
            public const byte Integer64_8 = 0x2b;
            public const byte Integer64_8_Negate = 0x2c;
            public const byte Integer64_0 = 0x2d;

            public const byte Integer64_64_u = 0x2e;
            public const byte Integer64_56_u = 0x2f;
            public const byte Integer64_48_u = 0x30;
            public const byte Integer64_40_u = 0x31;
            public const byte Integer64_32_u = 0x32;
            public const byte Integer64_24_u = 0x33;
            public const byte Integer64_16_u = 0x34;
            public const byte Integer64_8_u = 0x35;
            public const byte Integer64_0_u = 0x36;


            public const byte Byte = 0x37;
            public const byte BoolTrue = 0x38;
            public const byte BoolFalse = 0x39;
            public const byte Charter8 = 0x3a;
            public const byte Charter32 = 0x3b;
            public const byte Float32 = 0x3c;
            public const byte Float64 = 0x3d;
            public const byte SymbolShort = 0x3e;
            public const byte SymbolLong = 0x3f;

            public const byte SymbolIndex = 0x40;

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

        private static byte[] X1 = {0x00};
        private static byte[] X2 = { 0x00, 0x00 };
        private static byte[] X3 = { 0x00, 0x00, 0x00 };
        private static byte[] X4 = { 0x00, 0x00, 0x00, 0x00 };
        private static byte[] X5 = { 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static byte[] X6 = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static byte[] X7 = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        private static byte[] XN1 = { 0xFF };
        private static byte[] XN2 = { 0xFF, 0xFF };
        private static byte[] XN3 = { 0xFF, 0xFF, 0xFF };
        private static byte[] XN4 = { 0xFF, 0xFF, 0xFF, 0xFF };
        private static byte[] XN5 = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        private static byte[] XN6 = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        private static byte[] XN7 = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        public static Cons Read(MemoryStream data, SymbolTable symbolTable) {
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
                    case Codes.Integer16_16_u:
                        set(BitConverter.ToUInt16(R(data, 2), 0));
                        break;
                    case Codes.Integer16_8_u:
                        set(BitConverter.ToUInt16(R(data, 1, X1), 0));
                        break;
                    case Codes.Integer16_0_u:
                        set((ushort)0);
                        break;
                    case Codes.Integer32_32:
                        set(BitConverter.ToInt32(R(data, 4), 0));
                        break;
                    case Codes.Integer32_24:
                        set(BitConverter.ToInt32(R(data, 3, X1), 0));
                        break;
                    case Codes.Integer32_24_Negate:
                        set(BitConverter.ToInt32(R(data, 3, XN1), 0));
                        break;
                    case Codes.Integer32_16:
                        set(BitConverter.ToInt32(R(data, 2, X2), 0));
                        break;
                    case Codes.Integer32_16_Negate:
                        set(BitConverter.ToInt32(R(data, 2, XN2), 0));
                        break;
                    case Codes.Integer32_8:
                        set(BitConverter.ToInt32(R(data, 1, X3), 0));
                        break;
                    case Codes.Integer32_8_Negate:
                        set(BitConverter.ToInt32(R(data, 1, XN3), 0));
                        break;
                    case Codes.Integer32_0:
                        set(0);
                        break;
                    case Codes.Integer32_32_u:
                        set(BitConverter.ToUInt32(R(data, 4), 0));
                        break;
                    case Codes.Integer32_24_u:
                        set(BitConverter.ToUInt32(R(data, 3, X1), 0));
                        break;
                    case Codes.Integer32_16_u:
                        set(BitConverter.ToUInt32(R(data, 2, X2), 0));
                        break;
                    case Codes.Integer32_8_u:
                        set(BitConverter.ToUInt32(R(data, 1, X3), 0));
                        break;
                    case Codes.Integer32_0_u:
                        set(0u);
                        break;
                    case Codes.Integer64_64:
                        set(BitConverter.ToInt64(R(data, 8), 0));
                        break;
                    case Codes.Integer64_56:
                        set(BitConverter.ToInt64(R(data, 7, X1), 0));
                        break;
                    case Codes.Integer64_56_Negate:
                        set(BitConverter.ToInt64(R(data, 7, XN1), 0));
                        break;
                    case Codes.Integer64_48:
                        set(BitConverter.ToInt64(R(data, 6, X2), 0));
                        break;
                    case Codes.Integer64_48_Negate:
                        set(BitConverter.ToInt64(R(data, 6, XN2), 0));
                        break;
                    case Codes.Integer64_40:
                        set(BitConverter.ToInt64(R(data, 5, X3), 0));
                        break;
                    case Codes.Integer64_40_Negate:
                        set(BitConverter.ToInt64(R(data, 5, XN3), 0));
                        break;
                    case Codes.Integer64_32:
                        set(BitConverter.ToInt64(R(data, 4, X4), 0));
                        break;
                    case Codes.Integer64_32_Negate:
                        set(BitConverter.ToInt64(R(data, 4, XN4), 0));
                        break;
                    case Codes.Integer64_24:
                        set(BitConverter.ToInt64(R(data, 3, X5), 0));
                        break;
                    case Codes.Integer64_24_Negate:
                        set(BitConverter.ToInt64(R(data, 3, XN5), 0));
                        break;
                    case Codes.Integer64_16:
                        set(BitConverter.ToInt64(R(data, 2, X6), 0));
                        break;
                    case Codes.Integer64_16_Negate:
                        set(BitConverter.ToInt64(R(data, 2, XN6), 0));
                        break;
                    case Codes.Integer64_8:
                        set(BitConverter.ToInt64(R(data, 1, X7), 0));
                        break;
                    case Codes.Integer64_8_Negate:
                        set(BitConverter.ToInt64(R(data, 1, XN7), 0));
                        break;
                    case Codes.Integer64_0:
                        set(0L);
                        break;
                    case Codes.Integer64_64_u:
                        set(BitConverter.ToUInt64(R(data, 8), 0));
                        break;
                    case Codes.Integer64_56_u:
                        set(BitConverter.ToUInt64(R(data, 7, X1), 0));
                        break;
                    case Codes.Integer64_48_u:
                        set(BitConverter.ToUInt64(R(data, 6, X2), 0));
                        break;
                    case Codes.Integer64_40_u:
                        set(BitConverter.ToUInt64(R(data, 5, X3), 0));
                        break;
                    case Codes.Integer64_32_u:
                        set(BitConverter.ToUInt64(R(data, 4, X4), 0));
                        break;
                    case Codes.Integer64_24_u:
                        set(BitConverter.ToUInt64(R(data, 3, X5), 0));
                        break;
                    case Codes.Integer64_16_u:
                        set(BitConverter.ToUInt64(R(data, 2, X6), 0));
                        break;
                    case Codes.Integer64_8_u:
                        set(BitConverter.ToUInt64(R(data, 1, X7), 0));
                        break;
                    case Codes.Integer64_0_u:
                        set(0UL);
                        break;
                    case Codes.Byte:
                        set(data.ReadByte());
                        break;
                    case Codes.BoolTrue:
                        set(true);
                        break;
                    case Codes.BoolFalse:
                        set(false);
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
                    case Codes.SymbolIndex:
                        set(new Symbol(symbolTable?.Get(BitConverter.ToUInt16(R(data, 2), 0))));
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

        public static MemoryStream Write(object tree, SymbolTable symbolTable) {
            var ms = new MemoryStream();

            WriteItem(ms, tree, symbolTable);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        private static MemoryStream WriteConsSeq(MemoryStream ms, Cons tree, SymbolTable symbolTable) {
            ms.WriteByte(Codes.Open);

            foreach (var o in tree.GetIterator()) {
                WriteItem(ms, o.Car(), symbolTable);

                var cdr = o.Cdr();

                if (cdr is Cons || (cdr == null)) continue;

                ms.WriteByte(Codes.Dot);
                WriteItem(ms, cdr, symbolTable);
            }

            ms.WriteByte(Codes.Close);

            return ms;
        }

        private static MemoryStream WriteItem(MemoryStream ms, object item, SymbolTable symbolTable) {
            var writeCode = new Action<byte>(ms.WriteByte);

            if (item is Cons) {
                WriteConsSeq(ms, item as Cons, symbolTable);
            } else if (item is int) {
                var val = (int)item;
                if (val == 0) {
                    writeCode(Codes.Integer32_0);
                } else if ((val <= 255) && (val >= -255)) {
                    writeCode(val > 0 ? Codes.Integer32_8 : Codes.Integer32_8_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else if ((val <= 65535) && (val >= -65535)) {
                    writeCode(val > 0 ? Codes.Integer32_16 : Codes.Integer32_16_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(2).ToArray());
                } else if ((val <= 16777215) && (val >= -16777215)) {
                    writeCode(val > 0 ? Codes.Integer32_24 : Codes.Integer32_24_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(3).ToArray());
                } else {
                    writeCode(Codes.Integer32_32);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is uint) {
                var val = (uint)item;
                if (val == 0) {
                    writeCode(Codes.Integer32_0_u);
                } else if ((val <= 255)) {
                    writeCode(Codes.Integer32_8_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else if ((val <= 65535)) {
                    writeCode(Codes.Integer32_16_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(2).ToArray());
                } else if ((val <= 16777215)) {
                    writeCode(Codes.Integer32_24_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(3).ToArray());
                } else {
                    writeCode(Codes.Integer32_32_u);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is long) {
                var val = (long)item;
                if (val == 0) {
                    writeCode(Codes.Integer64_0);
                } else if ((val <= 255) && (val >= -255)) {
                    writeCode(val > 0 ? Codes.Integer64_8 : Codes.Integer64_8_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else if ((val <= 65535) && (val >= -65535)) {
                    writeCode(val > 0 ? Codes.Integer64_16 : Codes.Integer64_16_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(2).ToArray());
                } else if ((val <= 16777215) && (val >= -16777215)) {
                    writeCode(val > 0 ? Codes.Integer64_24 : Codes.Integer64_24_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(3).ToArray());
                } else if ((val <= 4294967295) && (val >= -4294967295)) {
                    writeCode(val > 0 ? Codes.Integer64_32 : Codes.Integer64_32_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(4).ToArray());
                } else if ((val <= 1099511627775) && (val >= -1099511627775)) {
                    writeCode(val > 0 ? Codes.Integer64_40 : Codes.Integer64_40_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(5).ToArray());
                } else if ((val <= 281474976710655) && (val >= -281474976710655)) {
                    writeCode(val > 0 ? Codes.Integer64_48 : Codes.Integer64_48_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(6).ToArray());
                } else if ((val <= 72057594037927935) && (val >= -72057594037927935)) {
                    writeCode(val > 0 ? Codes.Integer64_56 : Codes.Integer64_56_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(7).ToArray());
                } else {
                    writeCode(Codes.Integer64_64);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is ulong) {
                var val = (ulong)item;
                if (val == 0) {
                    writeCode(Codes.Integer64_0_u);
                } else if ((val <= 255)) {
                    writeCode(Codes.Integer64_8_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else if ((val <= 65535)) {
                    writeCode(Codes.Integer64_16_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(2).ToArray());
                } else if ((val <= 16777215)) {
                    writeCode(Codes.Integer64_24_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(3).ToArray());
                } else if ((val <= 4294967295)) {
                    writeCode(Codes.Integer64_32_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(4).ToArray());
                } else if ((val <= 1099511627775)) {
                    writeCode(Codes.Integer64_40_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(5).ToArray());
                } else if ((val <= 281474976710655)) {
                    writeCode(Codes.Integer64_48_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(6).ToArray());
                } else if ((val <= 72057594037927935)) {
                    writeCode(Codes.Integer64_56_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(7).ToArray());
                } else {
                    writeCode(Codes.Integer64_64_u);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is short) {
                var val = (short)item;
                if (val == 0) {
                    writeCode(Codes.Integer16_0);
                } else if ((val <= 255) && (val >= -255)) {
                    writeCode(val > 0 ? Codes.Integer16_8 : Codes.Integer16_8_Negate);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else {
                    writeCode(Codes.Integer16_16);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
            } else if (item is ushort) {
                var val = (ushort)item;
                if (val == 0) {
                    writeCode(Codes.Integer16_0_u);
                } else if (val <= 255) {
                    writeCode(Codes.Integer16_8_u);
                    MsWrite(ms, BitConverter.GetBytes(val).Take(1).ToArray());
                } else {
                    writeCode(Codes.Integer16_16_u);
                    MsWrite(ms, BitConverter.GetBytes(val));
                }
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
                writeCode((bool)item ? Codes.BoolTrue : Codes.BoolFalse);
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
                var name = (item as Symbol).Name;

                var index = symbolTable?.Get(name);

                if (index.HasValue) {
                    writeCode(Codes.SymbolIndex);
                    MsWrite(ms, BitConverter.GetBytes((ushort)index.Value));
                }
                else {
                    var bytes = Encoding.UTF8.GetBytes(name);
                    if (bytes.Length <= 255) {
                        writeCode(Codes.SymbolShort);
                        ms.WriteByte((byte) bytes.Length);
                    }
                    else {
                        writeCode(Codes.SymbolLong);
                        MsWrite(ms, BitConverter.GetBytes((short) bytes.Length));
                    }
                    MsWrite(ms, bytes);
                }
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

    public class SymbolTable {
        private readonly Dictionary<string, ushort> _indexer;
        private readonly List<string> _symbols;

        public SymbolTable() {
            _indexer = new Dictionary<string, ushort>();
            _symbols = new List<string>();
        }

        public SymbolTable(string[] symbols) {
            _indexer = new Dictionary<string, ushort>();
            _symbols = new List<string>();

            foreach (var symbol in symbols) {
                _indexer[symbol] = (ushort)_symbols.Count;
                _symbols.Add(symbol);
            }
        }

        public ushort? Get(string symbolname) {
            if (_indexer.ContainsKey(symbolname)) {
                return _indexer[symbolname];
            }

            return null;
        }

        public string Get(ushort index) {
            if (index < _symbols.Count) {
                var tmp = _symbols[index];
                return _symbols[index];
            }

            return null;
        }
    }
}
