namespace Redpoint.OpenGE.LexerParser.LineParsing
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    internal ref struct ScanningLindexLine
    {
        [FieldOffset(0)]
        public uint Offset;

        [FieldOffset(4)]
        public uint Length;

        [FieldOffset(0)]
        internal unsafe ScanningLindexPage _nextPage;
    }
}
