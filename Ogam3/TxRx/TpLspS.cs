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
