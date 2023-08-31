namespace Redpoint.OpenGE.Core.Tests
{
    using System.Text;
    using Xunit;

    public class SequentialVersion1Tests
    {
        [Fact]
        public void TestEncodingAndDecoding()
        {
            var bigFileStringBuilder = new StringBuilder();
            for (int i = 0; i < 128 * 1024 / 10; i++)
            {
                bigFileStringBuilder.Append("this is a big file");
            }
            var bigFileString = bigFileStringBuilder.ToString();
            var bigFile = Encoding.UTF8.GetBytes(bigFileString);

            var entries = new Dictionary<long, BlobInfo>
            {
                {
                    128,
                    new BlobInfo
                    {
                        Content = "hello world",
                        Path = null,
                        ByteLength = Encoding.UTF8.GetBytes("hello world").Length,
                    }
                },
                {
                    8375,
                    new BlobInfo
                    {
                        Content = bigFileString,
                        Path = null,
                        ByteLength = bigFile.LongLength,
                    }
                },
                {
                    534,
                    new BlobInfo
                    {
                        Content = "6D%F&^Thgfjshdgfjh",
                        Path = null,
                        ByteLength =  Encoding.UTF8.GetBytes("6D%F&^Thgfjshdgfjh").Length,
                    }
                }
            };

            var memory = new MemoryStream();
            using (var encoder = new SequentialVersion1Encoder(entries, entries.Keys))
            {
                encoder.CopyTo(memory);
            }

            long currentHash = 0;
            int currentLength = 0;
            int currentRemaining = 0;
            var newBuffer = new byte[256 * 1024];
            var readEntries = new Dictionary<long, string>();

            using (var decoder = new SequentialVersion1Decoder(
                (hash, length) =>
                {
                    currentHash = hash;
                    currentLength = (int)length;
                    currentRemaining = (int)length;
                },
                (hash, blobOffset, buffer, bufferOffset, bufferCount) =>
                {
                    Array.Copy(
                        buffer,
                        bufferOffset,
                        newBuffer,
                        blobOffset,
                        bufferCount);
                    currentRemaining -= bufferCount;
                    if (currentRemaining == 0)
                    {
                        readEntries[currentHash] = Encoding.UTF8.GetString(newBuffer, 0, currentLength);
                    }
                }))
            {
                memory.Seek(0, SeekOrigin.Begin);
                memory.CopyTo(decoder);
            }

            Assert.Equal(3, readEntries.Count);
            Assert.True(readEntries.ContainsKey(128));
            Assert.Equal("hello world", readEntries[128]);
            Assert.True(readEntries.ContainsKey(8375));
            Assert.Equal(bigFileString, readEntries[8375]);
            Assert.True(readEntries.ContainsKey(534));
            Assert.Equal("6D%F&^Thgfjshdgfjh", readEntries[534]);
        }
    }
}