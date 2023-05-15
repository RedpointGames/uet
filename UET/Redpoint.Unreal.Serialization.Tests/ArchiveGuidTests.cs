namespace Redpoint.Unreal.Serialization.Tests
{
    using System.Globalization;

    public class ArchiveGuidTests
    {
        private const uint _ae = 3222294595;
        private const uint _be = 1199471771;
        private const uint _ce = 4080516521;
        private const uint _de = 932217854;
        private static string _base = "Q1AQwJt8fkepvTfz/oOQNw==";
        private static string _unrealGuid = "C0105043477E7C9BF337BDA9379083FE";

        private Guid UnrealGuidToMicrosoftGuid(string hexString)
        {
            byte[] data = new byte[hexString.Length / 2];
            for (int index = 0; index < data.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return new Guid(data);
        }

        [Fact]
        public void DeserializesFromUnrealToUint32()
        {
            var bytes = Convert.FromBase64String(_base);
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                uint a = 0, b = 0, c = 0, d = 0;
                archive.Serialize(ref a);
                archive.Serialize(ref b);
                archive.Serialize(ref c);
                archive.Serialize(ref d);

                Assert.Equal(_ae, a);
                Assert.Equal(_be, b);
                Assert.Equal(_ce, c);
                Assert.Equal(_de, d);
            }
        }

        [Fact]
        public void DeserializesFromUnreal()
        {
            var bytes = Convert.FromBase64String(_base);
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                Guid guid = Guid.Empty;
                archive.Serialize(ref guid);

                Assert.Equal(BitConverter.ToString(UnrealGuidToMicrosoftGuid(_unrealGuid).ToByteArray()), BitConverter.ToString(guid.ToByteArray()));
            }
        }

        [Fact]
        public void SerializesToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                Guid guid = UnrealGuidToMicrosoftGuid(_unrealGuid);
                archive.Serialize(ref guid);

                Assert.Equal(
                    _base,
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}