namespace Redpoint.OpenGE.LexerParser.LineParsing
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    internal ref struct ScanningLindexPage
    {
        [FieldOffset(0)]
        internal unsafe ScanningLindexLine* _page;

        internal unsafe ScanningLindexPage(ScanningLindexLine* page)
        {
            _page = page;
        }

        internal const int _blockSize = 16 * 1024;
        internal const int _structSize = 8;
        internal const int _pageSize = _blockSize / _structSize;
        internal const int _nextPageIndex = _pageSize - 1;
        public const int Size = _nextPageIndex;

        public static ref ScanningLindexPage NextPage(ref ScanningLindexPage page, out bool hadNext, bool shouldAllocate = false)
        {
            unsafe
            {
                ref var next = ref page._page[ScanningLindexPage._nextPageIndex]._nextPage;
                if (next._page == null)
                {
                    if (shouldAllocate)
                    {
                        next._page = ScanningLindexDocument.Allocate();
                        hadNext = true;
                        return ref next;
                    }
                    hadNext = false;
                    return ref page;
                }
                hadNext = true;
                return ref next;
            }
        }

        public static ref ScanningLindexLine Get(ref ScanningLindexPage page, int line)
        {
            if (line < 0)
            {
                throw new ArgumentException("line must be a positive or zero value.", nameof(line));
            }
            return ref Get(ref page, (uint)line);
        }

        public static ref ScanningLindexLine Get(ref ScanningLindexPage page, uint line)
        {
            unsafe
            {
                var ptr = page._page;
                if (ptr == null)
                {
                    throw new ArgumentNullException(nameof(page), $"{nameof(ScanningLindexPage)} should only be obtained from {nameof(ScanningLindexDocument)}");
                }
                if (line >= Size)
                {
                    throw new ArgumentNullException(nameof(line), $"{nameof(line)} must not exceed {Size - 1}");
                }
                return ref ptr[line];
            }
        }
    }
}
