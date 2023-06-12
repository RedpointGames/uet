namespace Redpoint.Vfs.Abstractions.Tests
{
    public class DirectoryAggregationTests
    {
        private VfsEntry SimpleEntry(string name)
        {
            return new VfsEntry
            {
                Name = name,
                Attributes = FileAttributes.Archive,
                CreationTime = DateTime.Now,
                LastAccessTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
                ChangeTime = DateTime.Now,
                Size = 0,
            };
        }

        [Fact]
        public void SimpleAggregationWorks()
        {
            var a = new[]
            {
                SimpleEntry("a"),
                SimpleEntry("b"),
                SimpleEntry("c"),
            };
            var b = new[]
            {
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            var aggregated = DirectoryAggregation.Aggregate(a, b, true).ToList();
            Assert.Single(aggregated, x => x.Name == "a");
            Assert.Single(aggregated, x => x.Name == "b");
            Assert.Single(aggregated, x => x.Name == "c");
            Assert.Single(aggregated, x => x.Name == "d");
            Assert.Single(aggregated, x => x.Name == "e");
        }

        [Fact]
        public void CorrectnessCheckCatchesUnsortedUpstream()
        {
            var a = new[]
            {
                SimpleEntry("c"),
                SimpleEntry("b"),
                SimpleEntry("a"),
            };
            var b = new[]
            {
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            Assert.Throws<CorrectnessCheckFailureException>(() =>
            {
                _ = DirectoryAggregation.Aggregate(a, b, true).ToList();
            });
        }

        [Fact]
        public void CorrectnessCheckCatchesUnsortedLocal()
        {
            var a = new[]
            {
                SimpleEntry("a"),
                SimpleEntry("b"),
                SimpleEntry("c"),
            };
            var b = new[]
            {
                SimpleEntry("e"),
                SimpleEntry("d"),
                SimpleEntry("c"),
            };

            Assert.Throws<CorrectnessCheckFailureException>(() =>
            {
                _ = DirectoryAggregation.Aggregate(a, b, true).ToList();
            });
        }

        [Fact]
        public void CorrectnessCheckCatchesUnsortedLocalWithNullUpstream()
        {
            var b = new[]
            {
                SimpleEntry("e"),
                SimpleEntry("d"),
                SimpleEntry("c"),
            };

            Assert.Throws<CorrectnessCheckFailureException>(() =>
            {
                _ = DirectoryAggregation.Aggregate(null, b, true).ToList();
            });
        }

        [Fact]
        public void NullUpstreamEnumeratesSuccessfully()
        {
            var b = new[]
            {
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            var aggregated = DirectoryAggregation.Aggregate(null, b, true).ToList();
            Assert.Single(aggregated, x => x.Name == "c");
            Assert.Single(aggregated, x => x.Name == "d");
            Assert.Single(aggregated, x => x.Name == "e");
        }

        [Fact]
        public void LocalAlwaysOmitsScratchDbEntry()
        {
            var a = new[]
            {
                SimpleEntry(".uefs.db"),
                SimpleEntry("a"),
                SimpleEntry("b"),
                SimpleEntry("c"),
            };
            var b = new[]
            {
                SimpleEntry(".uefs.db"),
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            var aggregated = DirectoryAggregation.Aggregate(a, b, true).ToList();
            Assert.Single(aggregated, x => x.Name == "a");
            Assert.Single(aggregated, x => x.Name == "b");
            Assert.Single(aggregated, x => x.Name == "c");
            Assert.Single(aggregated, x => x.Name == "d");
            Assert.Single(aggregated, x => x.Name == "e");
        }

        [Fact]
        public void DotValuesHaveCorrectSortOrder()
        {
            var a = new[]
            {
                SimpleEntry("."),
                SimpleEntry(".."),
                SimpleEntry("a"),
                SimpleEntry("b"),
                SimpleEntry("c"),
            };
            var b = new[]
            {
                SimpleEntry("."),
                SimpleEntry(".."),
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            var aggregated = DirectoryAggregation.Aggregate(a, b, true).ToList();
            Assert.Single(aggregated, x => x.Name == ".");
            Assert.Single(aggregated, x => x.Name == "..");
            Assert.Single(aggregated, x => x.Name == "a");
            Assert.Single(aggregated, x => x.Name == "b");
            Assert.Single(aggregated, x => x.Name == "c");
            Assert.Single(aggregated, x => x.Name == "d");
            Assert.Single(aggregated, x => x.Name == "e");
        }

        [Fact]
        public void TreatsUnderscoreAsAfterDotDot()
        {
            var b = new[]
            {
                SimpleEntry("."),
                SimpleEntry(".."),
                SimpleEntry("__a__"),
                SimpleEntry("c"),
                SimpleEntry("d"),
                SimpleEntry("e"),
            };

            var aggregated = DirectoryAggregation.Aggregate(null, b, true).ToList();
            Assert.Single(aggregated, x => x.Name == ".");
            Assert.Single(aggregated, x => x.Name == "..");
            Assert.Single(aggregated, x => x.Name == "__a__");
            Assert.Single(aggregated, x => x.Name == "c");
            Assert.Single(aggregated, x => x.Name == "d");
            Assert.Single(aggregated, x => x.Name == "e");
        }
    }
}