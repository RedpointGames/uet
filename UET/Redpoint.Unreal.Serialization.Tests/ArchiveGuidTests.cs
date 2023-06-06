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
        public async Task DeserializesFromUnrealToUint32()
        {
            var bytes = Convert.FromBase64String(_base);
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                Store<uint> a = new Store<uint>(0),
                    b = new Store<uint>(0),
                    c = new Store<uint>(0),
                    d = new Store<uint>(0);
                await archive.Serialize(a);
                await archive.Serialize(b);
                await archive.Serialize(c);
                await archive.Serialize(d);

                Assert.Equal(_ae, a.V);
                Assert.Equal(_be, b.V);
                Assert.Equal(_ce, c.V);
                Assert.Equal(_de, d.V);
            }
        }

        [Fact]
        public async Task DeserializesFromUnreal()
        {
            var bytes = Convert.FromBase64String(_base);
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                var guid = new Store<Guid>(Guid.Empty);
                await archive.Serialize(guid);

                Assert.Equal(BitConverter.ToString(UnrealGuidToMicrosoftGuid(_unrealGuid).ToByteArray()), BitConverter.ToString(guid.V.ToByteArray()));
            }
        }

        [Fact]
        public async Task SerializesToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                var guid = new Store<Guid>(UnrealGuidToMicrosoftGuid(_unrealGuid));
                await archive.Serialize(guid);

                Assert.Equal(
                    _base,
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}