using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ogam3.Utils {
    public enum SequentialGuidType {
        SequentialAsString,
        SequentialAsBinary,
        SequentialAtEnd
    }

    public static class SGuid {
        private static readonly RNGCryptoServiceProvider Rng = new RNGCryptoServiceProvider();
        private static uint counter = 0;

        public static Guid NewSequentialGuid(SequentialGuidType guidType) {

            var randomBytes = new byte[10];
            Rng.GetBytes(randomBytes);

            lock (Rng) {
                counter++;
                randomBytes[0] = (byte)counter;
                randomBytes[1] = (byte)(counter >> 8);
            }

            var timestamp = DateTime.Now.Ticks / 10000L;
            var timestampBytes = BitConverter.GetBytes(timestamp);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(timestampBytes);
            }

            var guidBytes = new byte[16];

            switch (guidType) {
                case SequentialGuidType.SequentialAsString:
                case SequentialGuidType.SequentialAsBinary:

                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 0, 6);
                    Buffer.BlockCopy(randomBytes, 0, guidBytes, 6, 10);

                    // If formatting as a string, we have to reverse the order
                    // of the Data1 and Data2 blocks on little-endian systems.
                    if (guidType == SequentialGuidType.SequentialAsString == BitConverter.IsLittleEndian) {
                        Array.Reverse(guidBytes, 0, 4);
                        Array.Reverse(guidBytes, 4, 2);
                    }
                    break;

                case SequentialGuidType.SequentialAtEnd:

                    Buffer.BlockCopy(randomBytes, 0, guidBytes, 0, 10);
                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 10, 6);
                    break;
            }

            return new Guid(guidBytes);
        }

        private static readonly object ShortGuidLocker = new object();

        public static string ShortUID() {
            var acc = new StringBuilder(30);
            acc.Append(DateTime.Now.ToString("yyMMddhhmmssfff"));
            string randomString;
            lock (ShortGuidLocker) {
                randomString = RandomString(5);
            }
            acc.Append(randomString);
            return acc.ToString(); // 16 charater
        }

        private static readonly Random Random = new Random();
        private static string RandomString(int length) {

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        public static string GetSSGuid() {
            return NewSequentialGuid(SequentialGuidType.SequentialAsString).ToString("N");
        }

        public static ulong GetHashCodeInt64(string input) {
            var s1 = input.Substring(0, input.Length / 2);
            var s2 = input.Substring(input.Length / 2);

            var x = ((ulong)s1.GetHashCode()) << 32 | ((ulong)s2.GetHashCode());

            return x;
        }
    }
}
