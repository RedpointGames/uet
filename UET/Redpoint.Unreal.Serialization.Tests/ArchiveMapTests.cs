namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveMapTests
    {
        [Fact]
        public void StringStringMapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA/P///2YAbwBvAAAA/P///2IAYQByAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveMap<int, UnrealString, UnrealString> v = ArchiveMap<int, UnrealString, UnrealString>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Count);
                Assert.Equal("world", Assert.Contains("hello", (IDictionary<UnrealString, UnrealString>)v.Data));
                Assert.Equal("bar", Assert.Contains("foo", (IDictionary<UnrealString, UnrealString>)v.Data));
            }
        }

        [Fact]
        public void StringStringMapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveMap<int, UnrealString, UnrealString> v = new ArchiveMap<int, UnrealString, UnrealString>(new KeyValuePair<UnrealString, UnrealString>[] {
                    new ("hello", "world"),
                    new ("foo", "bar"),
                });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA/P///2YAbwBvAAAA/P///2IAYQByAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public void StringInt32MapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAAFAAAA/P///2YAbwBvAAAACgAAAA==");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveMap<int, UnrealString, int> v = ArchiveMap<int, UnrealString, int>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Count);
                Assert.Equal(5, Assert.Contains("hello", (IDictionary<UnrealString, int>)v.Data));
                Assert.Equal(10, Assert.Contains("foo", (IDictionary<UnrealString, int>)v.Data));
            }
        }

        [Fact]
        public void StringInt32MapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveMap<int, UnrealString, int> v = new ArchiveMap<int, UnrealString, int>(new KeyValuePair<UnrealString, int>[] {
                    new ("hello", 5),
                    new ("foo", 10),
                });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAAFAAAA/P///2YAbwBvAAAACgAAAA==",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public void Int32StringMapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAD6////dwBvAHIAbABkAAAACgAAAPz///9iAGEAcgAAAA==");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveMap<int, int, UnrealString> v = ArchiveMap<int, int, UnrealString>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Count);
                Assert.Equal("world", Assert.Contains(5, (IDictionary<int, UnrealString>)v.Data));
                Assert.Equal("bar", Assert.Contains(10, (IDictionary<int, UnrealString>)v.Data));
            }
        }

        [Fact]
        public void Int32StringMapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveMap<int, int, UnrealString> v = new ArchiveMap<int, int, UnrealString>(new KeyValuePair<int, UnrealString>[] {
                    new (5, "world"),
                    new (10, "bar"),
                });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAAUAAAD6////dwBvAHIAbABkAAAACgAAAPz///9iAGEAcgAAAA==",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public void Int32Int32MapFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAAPAAAACgAAABQAAAA=");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveMap<int, int, int> v = ArchiveMap<int, int, int>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Count);
                Assert.Equal(15, Assert.Contains(5, (IDictionary<int, int>)v.Data));
                Assert.Equal(20, Assert.Contains(10, (IDictionary<int, int>)v.Data));
            }
        }

        [Fact]
        public void Int32Int32MapToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveMap<int, int, int> v = new ArchiveMap<int, int, int>(new KeyValuePair<int, int>[] {
                    new (5, 15),
                    new (10, 20),
                });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAAUAAAAPAAAACgAAABQAAAA=",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}