namespace Redpoint.CloudFramework.Tests
{
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Repository;
    using System;
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

            var counterContainer = new CounterContainer
            {
                Value = await shardedCounters.GetAsync("test-sharded-counter").ConfigureAwait(true)
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
                            await shardedCounters.AdjustAsync("test-sharded-counter", adjustAmount).ConfigureAwait(true);
                            break;
                        }
                        catch (RpcException ex) when (ex.IsContentionException())
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
                var value = await shardedCounters.GetAsync("test-sharded-counter").ConfigureAwait(true);
                if (counterContainer.Value == value)
                {
                    Assert.True(true);
                    return;
                }
                await Task.Delay(1000).ConfigureAwait(true);
            }
            Assert.Equal(counterContainer.Value, await shardedCounters.GetAsync("test-sharded-counter").ConfigureAwait(true));
#pragma warning restore CA5394
        }
    }
}
