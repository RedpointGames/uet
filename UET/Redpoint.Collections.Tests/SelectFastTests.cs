namespace Redpoint.Collections.Tests
{
    using System.Runtime.CompilerServices;
    using Xunit;

    public class SelectFastTests
    {
        private static async IAsyncEnumerable<int> EnumerateBlocked(IEnumerable<int> values, Dictionary<int, SemaphoreSlim> semaphores, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var value in values)
            {
                await semaphores[value].WaitAsync(cancellationToken);
                yield return value;
            }
        }

        private static async IAsyncEnumerable<IEnumerable<int>> EnumerateSourceLinear()
        {
            yield return [1, 2, 3, 4];
            yield return [5, 6, 7, 8, 9, 10];
            yield return [11, 12];
            yield return [13, 14, 15];
        }

        private static async IAsyncEnumerable<IEnumerable<int>> EnumerateSourceHorizontal()
        {
            yield return [3, 8, 10, 12];
            yield return [2, 5, 9, 13, 14, 15];
            yield return [1, 7];
            yield return [4, 6, 11];
        }

        [Fact]
        public Task SelectManyFastLinear()
        {
            return SelectManyFast(EnumerateSourceLinear);
        }

        [Fact]
        public Task SelectManyFastHorizontal()
        {
            return SelectManyFast(EnumerateSourceHorizontal);
        }

        private async Task SelectManyFast(Func<IAsyncEnumerable<IEnumerable<int>>> enumerableFactory)
        {
            var semaphores = new Dictionary<int, SemaphoreSlim>();
            var ints = await enumerableFactory().SelectMany(x => x).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            foreach (var value in ints)
            {
                semaphores.Add(value, new SemaphoreSlim(0));
            }

            semaphores[1].Release();

            var received = new List<int>();
            await foreach (var value in enumerableFactory().SelectManyFast(8, x => EnumerateBlocked(x, semaphores, TestContext.Current.CancellationToken)))
            {
                TestContext.Current.TestOutputHelper!.WriteLine($"Got value {value}.");
                if (semaphores.TryGetValue(value + 1, out var semaphore))
                {
                    semaphore.Release();
                }
                received.Add(value);
            }

            Assert.Equal(
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
                received);
        }

        [Fact]
        public async Task SelectFast()
        {
            var inputs = new[]
            {
                1, 2, 3, 4, 5, 6
            };

            await inputs.ToAsyncEnumerable().SelectFast(async input =>
            {
                Assert.NotEqual(0, input);
                await Task.Delay(input * 10).ConfigureAwait(true);
                return input;
            }).ToListAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }
}