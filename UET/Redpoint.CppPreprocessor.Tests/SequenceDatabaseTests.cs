namespace Redpoint.CppPreprocessor.Tests
{
    using Redpoint.CppPreprocessor.Memory;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class SequenceDatabaseTests
    {
        [Fact]
        public void CanStoreAndRetrieveStrings()
        {
            var db = new SequenceDatabase<char>();
            var hello = db.Store("hello");
            var world = db.Store("world");
            Assert.Equal("hello", db[hello].ToString());
            Assert.Equal("world", db[world].ToString());
        }

        [Fact]
        public void CanStoreAndRetrieveLotsOfData()
        {
            var db = new SequenceDatabase<char>();
            var test = new Dictionary<ulong, string>();
            for (int i = 0; i < 20; i++)
            {
                var value = Random.Shared.Next().ToString();
                for (int j = 0; j < i; j++)
                {
                    value = value + value;
                }
                test.Add(db.Store(value), value);
            }
            foreach (var kv in test)
            {
                Assert.Equal(kv.Value, db[kv.Key].ToString());
            }
        }
    }
}
