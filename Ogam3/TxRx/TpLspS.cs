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

namespace Ogam3.TxRx {
    struct TpLspS {
        public static byte BEGIN = 1;
        public static byte PKGCOUNT = 3;
        public static byte DATA = 5;
        public static byte CHECK_HEADER = 6;
        public static byte CHECK_DATA = 7;
        public static byte ESCAPE = 8;

        public ulong Rap;
        public bool IsQuantizied;
        public uint DataLength;
        public uint QuantShift;
        public uint QuantId;
        public byte[] QuantData;
    }
}
