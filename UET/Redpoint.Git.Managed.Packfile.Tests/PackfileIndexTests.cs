namespace Redpoint.Git.Managed.Packfile.Tests
{
    using Redpoint.Numerics;

    public class PackfileIndexTests
    {
        [Fact]
        public void CanEnumeratePackfileIndex()
        {
            using var index = new PackfileIndex("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.idx");
            for (ushort i = 0; i < 256; i++)
            {
                _ = index.LowLevelGetNumberOfObjectsPrecedingSectionForMostSignificantByte((byte)i);
            }
            for (uint i = 0; i < index.ObjectCount; i++)
            {
                _ = index.GetShaForObjectIndex(i);
                _ = index.GetCrcForObjectIndex(i);
                _ = index.GetPackfileOffsetForObjectIndex(i);
            }
        }

        [Fact]
        public void CanBinarySearchPackfileIndex()
        {
            using var index = new PackfileIndex("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.idx");

            var orderedExpectedObjects = new (UInt160 sha, uint expectedOffset)[]
            {
                (UInt160.CreateFromString("380d18d2c40a6eeef9edd299ebcdfc6c4aa17cea"), 322),
                (UInt160.CreateFromString("4f273a7daeba8ad7ac7883dba39d5bacd0f75561"), 177),
                (UInt160.CreateFromString("557db03de997c86a4a028e1ebd3a1ceb225be238"), 156),
                (UInt160.CreateFromString("8007d41d5794e6ce4d4d2c97e370d5a9aa6d5213"), 138),
                (UInt160.CreateFromString("97e19f39cf8574b97cf276ffe376d7ebe974533a"), 205),
                (UInt160.CreateFromString("d6340facfbb763c6d516e7599eac94245fce52ec"), 12),
            };

            foreach (var expectedObject in orderedExpectedObjects)
            {
                Assert.True(index.GetObjectIndexForObjectSha(expectedObject.sha, out var objectIndex), $"Object {expectedObject.sha} should have been found.");
                var offsetInPackfile = index.GetPackfileOffsetForObjectIndex(objectIndex);
                Assert.Equal(expectedObject.expectedOffset, offsetInPackfile);
            }
        }

        [Fact]
        public void BinarySearchDoesNotReturnObjectsThatDoNotExist()
        {
            using var index = new PackfileIndex("pack-a3937f64bd05eea333e59ce57f47f3cdd76664b1.idx");

            var missingObjects = new[]
            {
                UInt160.CreateFromString("380d18d2c40a6eeef9edd299ebcdfc6c4aa17cdf"),
                UInt160.CreateFromString("380d18d2c40a6eeef9edd299ebcdfc6c4aa17ceb"),
                UInt160.CreateFromString("5f273a7daeba8ad7ac7883dba39d5bacd0f75561"),
                UInt160.CreateFromString("4f273a7daeba8ad7ac7983dba39d5bacd0f75561"),
                UInt160.CreateFromString("557db03de997c86a4a028e1ebd3a1ceb225bd238"),
                UInt160.CreateFromString("557db03de997c86a4a028e1ebd3a1ceb225bf238"),
                UInt160.CreateFromString("8007d41d5794e6ce4d4d2c97e370d5a9aa6d5223"),
                UInt160.CreateFromString("8007d41d5794e6ce4d4d2c97e370d5a9aa6d52f3"),
                UInt160.CreateFromString("0000000000000000000000000000000000000000"),
                UInt160.CreateFromString("ffffffffffffffffffffffffffffffffffffffff")
            };

            foreach (var missingObject in missingObjects)
            {
                Assert.False(index.GetObjectIndexForObjectSha(missingObject, out _), $"Object {missingObject} should not have been found.");
            }
        }
    }
}
