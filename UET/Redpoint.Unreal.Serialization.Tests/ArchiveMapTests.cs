namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveMapTests
    {
        [Fact]
        public async Task StringStringMapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA/P///2YAbwBvAAAA/P///2IAYQByAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, UnrealString, UnrealString>>(ArchiveMap<int, UnrealString, UnrealString>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Count);
                Assert.Equal("world", Assert.Contains("hello", (IDictionary<UnrealString, UnrealString>)v.V.Data));
                Assert.Equal("bar", Assert.Contains("foo", (IDictionary<UnrealString, UnrealString>)v.V.Data));
            }
        }

        [Fact]
        public async Task StringStringMapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, UnrealString, UnrealString>>(new ArchiveMap<int, UnrealString, UnrealString>(new KeyValuePair<UnrealString, UnrealString>[] {
                    new ("hello", "world"),
                    new ("foo", "bar"),
                }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA/P///2YAbwBvAAAA/P///2IAYQByAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public async Task StringInt32MapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAAFAAAA/P///2YAbwBvAAAACgAAAA==");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, UnrealString, int>>(ArchiveMap<int, UnrealString, int>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Count);
                Assert.Equal(5, Assert.Contains("hello", (IDictionary<UnrealString, int>)v.V.Data));
                Assert.Equal(10, Assert.Contains("foo", (IDictionary<UnrealString, int>)v.V.Data));
            }
        }

        [Fact]
        public async Task StringInt32MapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, UnrealString, int>>(new ArchiveMap<int, UnrealString, int>(new KeyValuePair<UnrealString, int>[] {
                    new ("hello", 5),
                    new ("foo", 10),
                }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAAFAAAA/P///2YAbwBvAAAACgAAAA==",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public async Task Int32StringMapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAD6////dwBvAHIAbABkAAAACgAAAPz///9iAGEAcgAAAA==");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, int, UnrealString>>(ArchiveMap<int, int, UnrealString>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Count);
                Assert.Equal("world", Assert.Contains(5, (IDictionary<int, UnrealString>)v.V.Data));
                Assert.Equal("bar", Assert.Contains(10, (IDictionary<int, UnrealString>)v.V.Data));
            }
        }

        [Fact]
        public async Task Int32StringMapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, int, UnrealString>>(new ArchiveMap<int, int, UnrealString>(new KeyValuePair<int, UnrealString>[] {
                    new (5, "world"),
                    new (10, "bar"),
                }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAAUAAAD6////dwBvAHIAbABkAAAACgAAAPz///9iAGEAcgAAAA==",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public async Task Int32Int32MapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAAPAAAACgAAABQAAAA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, int, int>>(ArchiveMap<int, int, int>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Count);
                Assert.Equal(15, Assert.Contains(5, (IDictionary<int, int>)v.V.Data));
                Assert.Equal(20, Assert.Contains(10, (IDictionary<int, int>)v.V.Data));
            }
        }

        [Fact]
        public async Task Int32Int32MapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false, new[] { new TestSerializerRegistry() });

                var v = new Store<ArchiveMap<int, int, int>>(new ArchiveMap<int, int, int>(new KeyValuePair<int, int>[] {
                    new (5, 15),
                    new (10, 20),
                }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAAUAAAAPAAAACgAAABQAAAA=",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}