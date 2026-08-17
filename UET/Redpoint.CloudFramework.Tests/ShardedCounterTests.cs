namespace Redpoint.CloudFramework.Tests
{
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Repository;
    using StackExchange.Redis;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
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

        [Fact]
        public async Task TestLuaScriptsForRedisCaching()
        {
            var redisDatabase = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            var fetchStoreToken = Guid.NewGuid().ToString();

            var keyA = new RedisKey("a");
            var keyB = new RedisKey("b");
            var keyC = new RedisKey("c");
            var keyD = new RedisKey("d");
            var keyE = new RedisKey("e");

            var tokenKeyA = new RedisKey("token-a");
            var tokenKeyB = new RedisKey("token-b");
            var tokenKeyC = new RedisKey("token-c");
            var tokenKeyD = new RedisKey("token-d");
            var tokenKeyE = new RedisKey("token-e");

            redisDatabase.StringSet(keyA, 0);
            redisDatabase.KeyDelete(keyB); // keyB not set
            redisDatabase.StringSet(keyC, 10);
            redisDatabase.KeyDelete(keyD); // keyD not set
            redisDatabase.StringSet(keyE, 20);

            var cachedValues = DefaultGlobalShardedCounter.RedisResultToArrayOfNumbers(await redisDatabase.ScriptEvaluateAsync(
                DefaultGlobalShardedCounter._loadExistingOrFlagFetchStoreOperation,
                [
                    keyA,
                    tokenKeyA,
                    keyB,
                    tokenKeyB,
                    keyC,
                    tokenKeyC,
                    keyD,
                    tokenKeyD,
                    keyE,
                    tokenKeyE,
                ],
                [
                    new RedisValue(fetchStoreToken)
                ]));
            Assert.Equal(
                new long?[]
                {
                    0,
                    null,
                    10,
                    null,
                    20
                },
                cachedValues);

            Assert.True(redisDatabase.StringGet(tokenKeyA).IsNull);
            Assert.Equal(fetchStoreToken, redisDatabase.StringGet(tokenKeyB).ToString());
            Assert.True(redisDatabase.StringGet(tokenKeyC).IsNull);
            Assert.Equal(fetchStoreToken, redisDatabase.StringGet(tokenKeyD).ToString());
            Assert.True(redisDatabase.StringGet(tokenKeyE).IsNull);

            await redisDatabase.ScriptEvaluateAsync(
                DefaultGlobalShardedCounter._adjustOrBreakFetchStoreOperation,
                [
                    keyB,
                    tokenKeyB,
                ],
                [
                    new RedisValue("200")
                ]);
            await redisDatabase.ScriptEvaluateAsync(
                DefaultGlobalShardedCounter._adjustOrBreakFetchStoreOperation,
                [
                    keyC,
                    tokenKeyC,
                ],
                [
                    new RedisValue("200")
                ]);

            Assert.Equal("0", redisDatabase.StringGet(keyA).ToString());
            Assert.True(redisDatabase.StringGet(keyB).IsNull);
            Assert.Equal("210", redisDatabase.StringGet(keyC).ToString());
            Assert.True(redisDatabase.StringGet(keyD).IsNull);
            Assert.Equal("20", redisDatabase.StringGet(keyE).ToString());

            Assert.True(redisDatabase.StringGet(tokenKeyA).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyB).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyC).IsNull);
            Assert.Equal(fetchStoreToken, redisDatabase.StringGet(tokenKeyD).ToString());
            Assert.True(redisDatabase.StringGet(tokenKeyE).IsNull);

            await redisDatabase.ScriptEvaluateAsync(
                DefaultGlobalShardedCounter._storeFetchedValueIfFetchStoreOperationUnbroken,
                [
                    keyB,
                    tokenKeyB,
                    keyD,
                    tokenKeyD,
                ],
                [
                    fetchStoreToken,
                    new RedisValue("5"),
                    new RedisValue("15"),
                ]);

            Assert.Equal("0", redisDatabase.StringGet(keyA).ToString());
            Assert.True(redisDatabase.StringGet(keyB).IsNull);
            Assert.Equal("210", redisDatabase.StringGet(keyC).ToString());
            Assert.Equal("15", redisDatabase.StringGet(keyD).ToString());
            Assert.Equal("20", redisDatabase.StringGet(keyE).ToString());

            Assert.True(redisDatabase.StringGet(tokenKeyA).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyB).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyC).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyD).IsNull);
            Assert.True(redisDatabase.StringGet(tokenKeyE).IsNull);
        }
    }
}
