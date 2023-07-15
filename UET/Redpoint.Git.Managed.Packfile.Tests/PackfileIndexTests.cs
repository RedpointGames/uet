namespace Redpoint.Git.Managed.Packfile.Tests
{
    public class PackfileIndexTests
    {
        [Fact]
        public void CanEnumeratePackfileIndex()
        {
            using var index = new PackfileIndex("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.idx");
            for (ushort i = 0; i < 256; i++)
            {
                _ = index.LowLevelGetObjectIndexAtFanoutIndex((byte)i);
            }
            for (uint i = 0; i < index.ObjectCount; i++)
            {
                _ = index.LowLevelGetShaAtObjectIndex(i);
                _ = index.LowLevelGetCrcAtObjectIndex(i);
                _ = index.LowLevelGetOffsetAtObjectIndex(i);
            }
        }
    }
}
