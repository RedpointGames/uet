namespace Redpoint.Git.Managed.Packfile.Tests
{
    using Redpoint.Numerics;
    using System.Text;

    public class PackfileTests
    {
        [Fact]
        public void CanReadBlobFromPackfile()
        {
            using var index = new PackfileIndex("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.idx");
            using var pack = new Packfile("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.pack");

            Assert.True(
                pack.GetRawPackfileEntry(
                    index,
                    UInt160.CreateFromString("557db03de997c86a4a028e1ebd3a1ceb225be238"),
                    out var type,
                    out var size,
                    out var stream),
                "Must be able to get raw packfile entry for known blob hash.");
            Assert.NotNull(stream);
            using (stream)
            {
                const ulong sizeWhenDecompressed = 12;

                Assert.Equal(GitObjectType.Blob, type);
                Assert.Equal(sizeWhenDecompressed, size);

                var decompressionBuffer = new byte[sizeWhenDecompressed];
                stream.ReadExactly(decompressionBuffer);

                Assert.Equal(-1, stream.ReadByte()); // We should be at the end of the stream.

                var content = Encoding.UTF8.GetString(decompressionBuffer);
                Assert.Equal("Hello World\n", content);
            }
        }
    }
}
