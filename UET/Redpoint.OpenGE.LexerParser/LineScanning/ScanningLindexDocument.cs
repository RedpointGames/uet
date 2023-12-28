namespace Redpoint.OpenGE.LexerParser.LineParsing
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    internal ref struct ScanningLindexDocument
    {
        internal unsafe ScanningLindexPage _firstPage;

        internal unsafe ScanningLindexDocument(ScanningLindexLine* firstPage)
        {
            _firstPage = new ScanningLindexPage(firstPage);
        }

        internal unsafe static ScanningLindexLine* Allocate()
        {
            var ptr = (ScanningLindexLine*)Marshal.AllocHGlobal(ScanningLindexPage._blockSize);
            Unsafe.InitBlockUnaligned(ptr, 0, ScanningLindexPage._blockSize);
            return ptr;
        }

        public static ScanningLindexDocument New()
        {
            unsafe
            {
                return new ScanningLindexDocument(Allocate());
            }
        }

        public static void Release(ref ScanningLindexDocument lines)
        {
            unsafe
            {
                var ptr = lines._firstPage._page;
                while (ptr != null)
                {
                    // @note: Not 'ref', since we will free the memory
                    // before moving to the next one!
                    var next = ptr[ScanningLindexPage._nextPageIndex]._nextPage._page;
                    Marshal.FreeHGlobal((nint)ptr);
                    ptr = next;
                }
                lines._firstPage._page = null;
            }
        }

        public static ref ScanningLindexPage GetFirstPage(ref ScanningLindexDocument document)
        {
            return ref document._firstPage;
        }

        public static ref ScanningLindexLine Get(ref ScanningLindexDocument lines, int line)
        {
            if (line < 0)
            {
                throw new ArgumentException("line must be a positive or zero value.", nameof(line));
            }
            return ref Get(ref lines, (uint)line);
        }

        public static ref ScanningLindexLine Get(ref ScanningLindexDocument lines, uint line)
        {
            unsafe
            {
                var ptr = lines._firstPage._page;
                if (ptr == null)
                {
                    throw new ArgumentNullException(nameof(lines), "LexerLines has already been released!");
                }
                if (line < ScanningLindexPage._nextPageIndex)
                {
                    return ref ptr[line];
                }
                while (line >= ScanningLindexPage._nextPageIndex)
                {
                    ref var next = ref ptr[ScanningLindexPage._nextPageIndex]._nextPage._page;
                    if (next == null)
                    {
                        // Since 'next' is ref, this also initializes NextPage.
                        next = Allocate();
                    }
                    ptr = next;
                    line -= ScanningLindexPage._nextPageIndex;
                }
                return ref ptr[line];
            }
        }
    }
}
