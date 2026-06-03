namespace Redpoint.CloudFramework.Tests
{
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    [Collection("CloudFramework Test")]
    public class ShardedCounterTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public const int DefaultDelayMs = 0;

        public ShardedCounterTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        class CounterContainer
        {
            public long Value { get; set; }
        }

        [Fact]
        public async Task TestShardedCounterBehavesCorrectlyUnderHighConcurrency()
        {
#pragma warning disable CA5394
            var shardedCounters = _env.Services.GetRequiredService<IShardedCounter>();
            var semaphore = new SemaphoreSlim(1);

            var counterName = new ShardedCounterName("test-sharded-counter");
            var counterContainer = new CounterContainer
            {
                Value = await shardedCounters.GetAsync(counterName).ConfigureAwait(true)
            };
            await Parallel.ForEachAsync(AsyncEnumerable.Range(0, 16), async (idx, ct) =>
            {
                for (int i = 0; i < 20; i++)
                {
                    var adjustAmount = Random.Shared.Next(-10, 10);
                    await semaphore.WaitAsync(ct).ConfigureAwait(true);
                    try
                    {
                        counterContainer.Value += adjustAmount;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    while (true)
                    {
                        try
                        {
                            await shardedCounters.AdjustAsync(counterName, adjustAmount).ConfigureAwait(true);
                            break;
                        }
                        catch (RpcException ex) when (ex.IsContentionException() || ex.StatusCode == StatusCode.Aborted)
                        {
                            await Task.Delay(Random.Shared.Next(0, 5) * 200, ct).ConfigureAwait(true);
                            continue;
                        }
                    }
                }
            }).ConfigureAwait(true);

            // Wait for Datastore to settle.
            for (int i = 0; i < 30; i++)
            {
                var value = await shardedCounters.GetAsync(counterName).ConfigureAwait(true);
                if (counterContainer.Value == value)
                {
                    Assert.True(true);
                    return;
                }
                await Task.Delay(1000, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
            Assert.Equal(counterContainer.Value, await shardedCounters.GetAsync(counterName).ConfigureAwait(true));
#pragma warning restore CA5394
        }

        [Fact]
        public async Task TestShardedCounterLoadMany()
        {
#pragma warning disable CA5394
            var globalShardedCounters = _env.Services.GetRequiredService<IGlobalShardedCounter>();

            var names = Enumerable.Range(0, 30).Select(x => new ShardedCounterName($"test-sharded-counter-{x}")).ToHashSet();
            foreach (var name in names)
            {
                await globalShardedCounters.AdjustAsync(string.Empty, name, Random.Shared.Next(10, 50)).ConfigureAwait(true);
            }

            // Wait for Datastore to settle.
            await Task.Delay(1000, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            var stopwatch = Stopwatch.StartNew();

            var loads = globalShardedCounters.GetManyAsync(string.Empty, names, TestContext.Current.CancellationToken);
            await Task.WhenAll(loads.Values);

            foreach (var name in names)
            {
                Assert.Contains(name, loads.Keys);
                Assert.True((await loads[name]) > 0, "Sharded counter should have non-zero value");
            }

            Debug.WriteLine($"Took {stopwatch.ElapsedMilliseconds} ms to get all counters.");
#pragma warning restore CA5394
        }
    }
}
