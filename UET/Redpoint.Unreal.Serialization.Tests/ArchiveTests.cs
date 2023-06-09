namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveTests
    {
        [Fact]
        public async Task DeserializesFromUnrealASCIIValue()
        {
            var bytes = Convert.FromBase64String("nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQAYAAABoZWxsbwA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new ISerializerRegistry[0]);

                var a = new Store<short>(0);
                var b = new Store<int>(0);
                var c = new Store<long>(0);
                var d = new Store<ushort>(0);
                var e = new Store<uint>(0);
                var f = new Store<ulong>(0);
                var g = new Store<float>(0);
                var h = new Store<double>(0);
                var i = new Store<string>(string.Empty);

                await archive.Serialize(a);
                await archive.Serialize(b);
                await archive.Serialize(c);
                await archive.Serialize(d);
                await archive.Serialize(e);
                await archive.Serialize(f);
                await archive.Serialize(g);
                await archive.Serialize(h);
                await archive.Serialize(i);

                Assert.Equal(-100, a.V);
                Assert.Equal(-200, b.V);
                Assert.Equal(-300, c.V);
                Assert.Equal(400u, d.V);
                Assert.Equal(500u, e.V);
                Assert.Equal(600u, f.V);
                Assert.Equal(700.0f, g.V);
                Assert.Equal(800.0, h.V);
                Assert.Equal("hello", i.V);
            }
        }

        [Fact]
        public async Task DeserializesFromUnrealUnicodeValue()
        {
            var bytes = Convert.FromBase64String("nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQPr///9oAGUAbABsAG8AAAA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new ISerializerRegistry[0]);

                var a = new Store<short>(0);
                var b = new Store<int>(0);
                var c = new Store<long>(0);
                var d = new Store<ushort>(0);
                var e = new Store<uint>(0);
                var f = new Store<ulong>(0);
                var g = new Store<float>(0);
                var h = new Store<double>(0);
                var i = new Store<string>(string.Empty);

                await archive.Serialize(a);
                await archive.Serialize(b);
                await archive.Serialize(c);
                await archive.Serialize(d);
                await archive.Serialize(e);
                await archive.Serialize(f);
                await archive.Serialize(g);
                await archive.Serialize(h);
                await archive.Serialize(i);

                Assert.Equal(-100, a.V);
                Assert.Equal(-200, b.V);
                Assert.Equal(-300, c.V);
                Assert.Equal(400u, d.V);
                Assert.Equal(500u, e.V);
                Assert.Equal(600u, f.V);
                Assert.Equal(700.0f, g.V);
                Assert.Equal(800.0, h.V);
                Assert.Equal("hello", i.V);
            }
        }

        [Fact]
        public async Task SerializesToUnrealUnicodeValue()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new ISerializerRegistry[0]);

                var a = new Store<short>(-100);
                var b = new Store<int>(-200);
                var c = new Store<long>(-300);
                var d = new Store<ushort>(400);
                var e = new Store<uint>(500u);
                var f = new Store<ulong>(600u);
                var g = new Store<float>(700.0f);
                var h = new Store<double>(800.0);
                var i = new Store<string>("hello");

                await archive.Serialize(a);
                await archive.Serialize(b);
                await archive.Serialize(c);
                await archive.Serialize(d);
                await archive.Serialize(e);
                await archive.Serialize(f);
                await archive.Serialize(g);
                await archive.Serialize(h);
                await archive.Serialize(i);

                Assert.Equal(
                    "nP84////1P7///////+QAfQBAABYAgAAAAAAAAAAL0QAAAAAAACJQPr///9oAGUAbABsAG8AAAA=",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}