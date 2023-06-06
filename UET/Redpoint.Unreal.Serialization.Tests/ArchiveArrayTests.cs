namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveArrayTests
    {
        [Fact]
        public async Task StringArrayFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                var v = new Store<ArchiveArray<int, UnrealString>>(ArchiveArray<int, UnrealString>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Length);
                Assert.Equal("hello", v.V.Data[0]);
                Assert.Equal("world", v.V.Data[1]);
            }
        }

        [Fact]
        public async Task StringArrayToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                var v = new Store<ArchiveArray<int, UnrealString>>(new ArchiveArray<int, UnrealString>(new UnrealString[] { "hello", "world" }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public async Task Int32ArrayFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAAKAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                var v = new Store<ArchiveArray<int, int>>(ArchiveArray<int, int>.Empty);

                await archive.Serialize(v);

                Assert.NotNull(v.V.Data);
                Assert.Equal(2, v.V.Data.Length);
                Assert.Equal(5, v.V.Data[0]);
                Assert.Equal(10, v.V.Data[1]);
            }
        }

        [Fact]
        public async Task Int32ArrayToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                var v = new Store<ArchiveArray<int, int>>(new ArchiveArray<int, int>(new[] { 5, 10 }));

                await archive.Serialize(v);

                Assert.Equal(
                    "AgAAAAUAAAAKAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}