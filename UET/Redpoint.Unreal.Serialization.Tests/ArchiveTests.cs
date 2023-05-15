namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveTests
    {
        [Fact]
        public void DeserializesFromUnrealASCIIValue()
        {
            var bytes = Convert.FromBase64String("nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQAYAAABoZWxsbwA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                short a = 0;
                int b = 0;
                long c = 0;
                ushort d = 0;
                uint e = 0;
                ulong f = 0;
                float g = 0;
                double h = 0;
                string i = string.Empty;

                archive.Serialize(ref a);
                archive.Serialize(ref b);
                archive.Serialize(ref c);
                archive.Serialize(ref d);
                archive.Serialize(ref e);
                archive.Serialize(ref f);
                archive.Serialize(ref g);
                archive.Serialize(ref h);
                archive.Serialize(ref i);

                Assert.Equal(-100, a);
                Assert.Equal(-200, b);
                Assert.Equal(-300, c);
                Assert.Equal(400u, d);
                Assert.Equal(500u, e);
                Assert.Equal(600u, f);
                Assert.Equal(700.0f, g);
                Assert.Equal(800.0, h);
                Assert.Equal("hello", i);
            }
        }

        [Fact]
        public void DeserializesFromUnrealUnicodeValue()
        {
            var bytes = Convert.FromBase64String("nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQPr///9oAGUAbABsAG8AAAA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                short a = 0;
                int b = 0;
                long c = 0;
                ushort d = 0;
                uint e = 0;
                ulong f = 0;
                float g = 0;
                double h = 0;
                string i = string.Empty;

                archive.Serialize(ref a);
                archive.Serialize(ref b);
                archive.Serialize(ref c);
                archive.Serialize(ref d);
                archive.Serialize(ref e);
                archive.Serialize(ref f);
                archive.Serialize(ref g);
                archive.Serialize(ref h);
                archive.Serialize(ref i);

                Assert.Equal(-100, a);
                Assert.Equal(-200, b);
                Assert.Equal(-300, c);
                Assert.Equal(400u, d);
                Assert.Equal(500u, e);
                Assert.Equal(600u, f);
                Assert.Equal(700.0f, g);
                Assert.Equal(800.0, h);
                Assert.Equal("hello", i);
            }
        }

        [Fact]
        public void SerializesToUnrealUnicodeValue()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                short a = -100;
                int b = -200;
                long c = -300;
                ushort d = 400;
                uint e = 500u;
                ulong f = 600u;
                float g = 700.0f;
                double h = 800.0;
                string i = "hello";

                archive.Serialize(ref a);
                archive.Serialize(ref b);
                archive.Serialize(ref c);
                archive.Serialize(ref d);
                archive.Serialize(ref e);
                archive.Serialize(ref f);
                archive.Serialize(ref g);
                archive.Serialize(ref h);
                archive.Serialize(ref i);

                Assert.Equal(
                    "nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQPr///9oAGUAbABsAG8AAAA=",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}