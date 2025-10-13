namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ClassificationTests
    {
        private static readonly int[] _sourceArray = new[] { 4, 5, 6 };
        private static readonly int[] _sourceArray2 = new[] {
                11,
                22,
                31,
                42,
                91,
                52,
                61,
                72,
                81,
            };

        [Fact]
        public void CreateClassifer()
        {
            var inputs = _sourceArray.ToAsyncEnumerable();
            
            inputs.Classify(x => x >= 5 ? "high" : "low");
        }

        [Fact]
        public void ConnectClassifer()
        {
            var inputs = _sourceArray.ToAsyncEnumerable();

            inputs.Classify(x => x >= 5 ? "high" : "low")
                .AndForClassification("low", input => input * 10)
                .AndForClassification("high", input => input * 100);
        }

        [Fact]
        public async Task IterateClassifier()
        {
            var inputs = _sourceArray.ToAsyncEnumerable();

            var results = await inputs.Classify(x => x >= 5 ? "high" : "low")
                .AndForClassification("low", input => input * 10)
                .AndForClassification("high", input => input * 100)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            Assert.Contains(40, results);
            Assert.Contains(500, results);
            Assert.Contains(600, results);
        }

        private class ObservePulledValues<T> : IAsyncEnumerable<T>
        {
            private readonly IAsyncEnumerable<T> _input;

            public ObservePulledValues(IAsyncEnumerable<T> input)
            {
                _input = input;
            }

            public List<T> PulledValues { get; } = new List<T>();

            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                await foreach (var value in _input.WithCancellation(cancellationToken))
                {
                    PulledValues.Add(value);
                    yield return value;
                }
            }
        }

        [Fact]
        public async Task CancellationTokenBehavesAsExpected()
        {
            var inputs = new ObservePulledValues<int>(_sourceArray2.ToAsyncEnumerable());

            await foreach (var value in inputs.ConfigureAwait(false))
            {
                if (value == 91)
                {
                    break;
                }
            }

            Assert.Contains(11, inputs.PulledValues);
            Assert.Contains(22, inputs.PulledValues);
            Assert.Contains(31, inputs.PulledValues);
            Assert.Contains(42, inputs.PulledValues);
            Assert.Contains(91, inputs.PulledValues);
            Assert.DoesNotContain(52, inputs.PulledValues);
            Assert.DoesNotContain(61, inputs.PulledValues);
            Assert.DoesNotContain(72, inputs.PulledValues);
            Assert.DoesNotContain(81, inputs.PulledValues);
        }

        [Fact]
        public async Task ClassifierOnlyPullsAlmostNeededValues()
        {
            var inputs = new ObservePulledValues<int>(_sourceArray2.ToAsyncEnumerable());

            var iteratedValues = new List<int>();

            await foreach (var value in 
                inputs.Classify(x => (x % 2 == 0) ? "2" : "1")
                    .AndForClassification("1", input => input * 10)
                    .AndForClassification("2", input => input * 100).ConfigureAwait(false))
            {
                iteratedValues.Add(value);

                if (value == 910)
                {
                    break;
                }
            }

            Assert.Equal(3, iteratedValues.Count);

            Assert.Contains(110, iteratedValues);
            Assert.Contains(310, iteratedValues);
            Assert.Contains(910, iteratedValues);

            Assert.Contains(11, inputs.PulledValues);
            Assert.Contains(22, inputs.PulledValues);
            Assert.Contains(31, inputs.PulledValues);
            Assert.Contains(42, inputs.PulledValues);
            Assert.Contains(91, inputs.PulledValues);
            Assert.DoesNotContain(52, inputs.PulledValues);
            Assert.DoesNotContain(61, inputs.PulledValues);
            Assert.DoesNotContain(72, inputs.PulledValues);
            Assert.DoesNotContain(81, inputs.PulledValues);
        }

        [Fact]
        public async Task ClassifierReturnsOtherValuesWithSlowProcessor()
        {
            var inputs = new ObservePulledValues<int>(_sourceArray2.ToAsyncEnumerable());

            var iteratedValues = new List<int>();

            await foreach (var value in
                inputs.Classify(x => (x % 2 == 0) ? "2" : "1")
                    .AndForClassificationAwait("1", async input => { await Task.Delay(10).ConfigureAwait(true); return input * 10; })
                    .AndForClassification("2", input => input * 100).ConfigureAwait(false))
            {
                iteratedValues.Add(value);

                if (value == 910)
                {
                    break;
                }
            }

            Assert.Equal(7, iteratedValues.Count);

            Assert.Contains(2200, iteratedValues);
            Assert.Contains(4200, iteratedValues);
            Assert.Contains(5200, iteratedValues);
            Assert.Contains(7200, iteratedValues);
            Assert.Contains(110, iteratedValues);
            Assert.Contains(310, iteratedValues);
            Assert.Contains(910, iteratedValues);
            Assert.DoesNotContain(610, iteratedValues);
            Assert.DoesNotContain(810, iteratedValues);

            Assert.Contains(11, inputs.PulledValues);
            Assert.Contains(22, inputs.PulledValues);
            Assert.Contains(31, inputs.PulledValues);
            Assert.Contains(42, inputs.PulledValues);
            Assert.Contains(91, inputs.PulledValues);
            Assert.Contains(52, inputs.PulledValues);
            Assert.Contains(61, inputs.PulledValues);
            Assert.Contains(72, inputs.PulledValues);
            Assert.Contains(81, inputs.PulledValues);
        }
    }
}
