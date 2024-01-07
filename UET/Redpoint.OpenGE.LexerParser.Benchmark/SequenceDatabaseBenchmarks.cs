namespace Redpoint.OpenGE.LexerParser.Benchmark
{
    using BenchmarkDotNet.Attributes;
    using Redpoint.OpenGE.LexerParser.Memory;
    using System;

#pragma warning disable CA5394

    [MemoryDiagnoser]
    public class SequenceDatabaseBenchmarks
    {
        private readonly SequenceDatabase<char> _sequenceDatabase;
        private readonly HashSet<string> _container;
        private const string _testString = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vestibulum a justo ut sem semper tempus id laoreet tortor. Pellentesque molestie hendrerit quam at iaculis. Aliquam at porttitor lectus, sed fermentum ipsum. Pellentesque non ultrices odio. Sed condimentum mollis erat at dapibus. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Cras orci risus, aliquet vel consectetur id, sodales ut nisi.";

        public SequenceDatabaseBenchmarks()
        {
            _sequenceDatabase = new SequenceDatabase<char>();
            _container = new HashSet<string>();
        }

        [Benchmark]
        public void StoreSpan()
        {
            _sequenceDatabase.Store(_testString.AsSpan());
        }

        [Benchmark]
        public void StoreString()
        {
            _container.Add(_testString);
        }

        [Benchmark]
        public void StoreAndRetrieveSpan()
        {
            var hash = _sequenceDatabase.Store(_testString.AsSpan());
            _ = _sequenceDatabase[hash];
        }

        [Benchmark]
        public void StoreAndRetrieveString()
        {
            _container.Add(_testString);
            _ = _container.First();
        }

        [Benchmark]
        public void StoreRandomSpan()
        {
            var s = _testString.AsSpan();
            var start = Random.Shared.Next(0, s.Length);
            var length = Random.Shared.Next(start, s.Length) - start;
            _sequenceDatabase.Store(s.Slice(start, length));
        }

        [Benchmark]
        public void StoreRandomString()
        {
            var start = Random.Shared.Next(0, _testString.Length);
            var length = Random.Shared.Next(start, _testString.Length) - start;
            _container.Add(_testString.Substring(start, length));
        }
    }
}
