namespace Redpoint.Unreal.Serialization.Tests
{
    public class ArchiveArrayTests
    {
        [Fact]
        public void StringArrayFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveArray<int, UnrealString> v = ArchiveArray<int, UnrealString>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Length);
                Assert.Equal("hello", v.Data[0]);
                Assert.Equal("world", v.Data[1]);
            }
        }

        [Fact]
        public void StringArrayToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveArray<int, UnrealString> v = new ArchiveArray<int, UnrealString>(new UnrealString[] { "hello", "world" });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAPr///9oAGUAbABsAG8AAAD6////dwBvAHIAbABkAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }

        [Fact]
        public void Int32ArrayFromUnreal()
        {
            var bytes = Convert.FromBase64String("AgAAAAUAAAAKAAAA");
            using (var stream = new MemoryStream(bytes))
            {
                var archive = new Archive(stream, true);

                ArchiveArray<int, int> v = ArchiveArray<int, int>.Empty;

                archive.Serialize(ref v);

                Assert.NotNull(v.Data);
                Assert.Equal(2, v.Data.Length);
                Assert.Equal(5, v.Data[0]);
                Assert.Equal(10, v.Data[1]);
            }
        }

        [Fact]
        public void Int32ArrayToUnreal()
        {
            using (var stream = new MemoryStream())
            {
                var archive = new Archive(stream, false);

                ArchiveArray<int, int> v = new ArchiveArray<int, int>(new[] { 5, 10 });

                archive.Serialize(ref v);

                Assert.Equal(
                    "AgAAAAUAAAAKAAAA",
                    Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}