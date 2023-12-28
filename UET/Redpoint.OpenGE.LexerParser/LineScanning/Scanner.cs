namespace Redpoint.OpenGE.LexerParser.LineScanning
{
    using Redpoint.OpenGE.LexerParser.LineParsing;
    using System;
    using System.IO.MemoryMappedFiles;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class Scanner
    {
        internal static ScanningLindexDocument GenerateLindexDocument(ScannerOpenFile file)
        {
            return GenerateLindexDocument(file.AsSpan());
        }

        internal static ScanningLindexDocument GenerateLindexDocument(ReadOnlySpan<byte> bytes)
        {
            var encoding = Encoding.UTF8;
            var characterBuffer = new char[encoding.GetMaxCharCount(bytes.Length)];
            encoding.GetChars(
                bytes,
                characterBuffer.AsSpan());
            var characterSpan = new ReadOnlySpan<char>(characterBuffer);
            ref var bufferStart = ref MemoryMarshal.GetReference(characterSpan);
            var document = ScanningLindexDocument.New();
            ref var page = ref ScanningLindexDocument.GetFirstPage(ref document);
            var indexInPage = 0;
            foreach (var line in characterSpan.EnumerateLines())
            {
                if (indexInPage >= ScanningLindexPage.Size)
                {
                    page = ref ScanningLindexPage.NextPage(ref page, out _, true);
                    indexInPage -= ScanningLindexPage.Size;
                }
                ref var lineValue = ref ScanningLindexPage.Get(ref page, indexInPage);
                ref var lineStart = ref MemoryMarshal.GetReference(line);
                lineValue.Offset = (uint)(Unsafe.ByteOffset(ref bufferStart, ref lineStart) / sizeof(char));
                lineValue.Length = (uint)line.Length;
                indexInPage++;
            }
            return document;
        }
    }
}
