namespace Redpoint.OpenGE.LexerParser.Tests
{
    using Redpoint.OpenGE.LexerParser.LineParsing;
    using Xunit;

    public class ScanningLindexTests
    {
        [Fact]
        public void TestAccess()
        {
            var page = ScanningLindexDocument.New();
            ref var a = ref ScanningLindexDocument.Get(ref page, 1000);
            a.Offset = 4000;
            ref var b = ref ScanningLindexDocument.Get(ref page, 1000);
            Assert.Equal(4000u, b.Offset);
            Assert.Equal(0u, b.Length);
            ScanningLindexDocument.Release(ref page);
        }

        [Fact]
        public void TestAllocate()
        {
            var page = ScanningLindexDocument.New();
            ref var a = ref ScanningLindexDocument.Get(ref page, 9000);
            a.Offset = 4000;
            ref var b = ref ScanningLindexDocument.Get(ref page, 9000);
            Assert.Equal(4000u, b.Offset);
            Assert.Equal(0u, b.Length);
            ScanningLindexDocument.Release(ref page);
        }

        [Fact]
        public void TestRange()
        {
            var page = ScanningLindexDocument.New();
            for (uint i = 0; i < 32000; i++)
            {
                ref var line = ref ScanningLindexDocument.Get(ref page, i);
                line.Offset = i;
                line.Length = uint.MaxValue - i;
            }
            for (uint i = 0; i < 32000; i++)
            {
                ref var line = ref ScanningLindexDocument.Get(ref page, i);
                Assert.Equal(i, line.Offset);
                Assert.Equal(uint.MaxValue - i, line.Length);
            }
            ScanningLindexDocument.Release(ref page);
        }
    }
}
