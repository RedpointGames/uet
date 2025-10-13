namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.Collections;
    using Redpoint.CloudFramework.Collections.Batching;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Xunit;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Tests.Models;

    [Collection("CloudFramework Test")]
    public class BatchingTests
    {
        class SecondaryObject
        {
        }

        class TestObject
        {
            public required int Index { get; init; }
            public required int KeyA { get; init; }
            public required int KeyB { get; init; }
        }

        class QueryableDictionary
        {
            private readonly Dictionary<int, SecondaryObject> _data;

            public QueryableDictionary(Dictionary<int, SecondaryObject> data)
            {
                _data = data;
            }

            public async IAsyncEnumerable<KeyValuePair<int, SecondaryObject?>> LookupByKey(IAsyncEnumerable<int> keys)
            {
                await Task.Yield();
                await foreach (var key in keys.ConfigureAwait(false))
                {
                    yield return new KeyValuePair<int, SecondaryObject?>(key, _data[key]);
                }
            }
        }

        private readonly CloudFrameworkTestEnvironment _env;

        public const int DefaultDelayMs = 0;

        public BatchingTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        [Fact]
        public async Task BatchJoinByKeyLookupAssociatesCorrectly()
        {
            var secondaryA = new Dictionary<int, SecondaryObject>();
            var secondaryB = new Dictionary<int, SecondaryObject>();
            for (int i = 0; i < 100; i++)
            {
                secondaryA.Add(i, new SecondaryObject());
                secondaryB.Add(i, new SecondaryObject());
            }

            var primary = new List<TestObject>();
            for (int i = 0; i < 500; i++)
            {
                primary.Add(new TestObject
                {
                    Index = i,
                    KeyA = RandomNumberGenerator.GetInt32(100),
                    KeyB = RandomNumberGenerator.GetInt32(100),
                });
            }

            var queryableA = new QueryableDictionary(secondaryA);
            var queryableB = new QueryableDictionary(secondaryB);

            var results = await primary
                .BatchInto(100)
                .ToAsyncEnumerable()
                .AsBatchedAsyncEnumerable()
                .JoinByDistinctKeyAwait(
                    p => p.KeyA,
                    queryableA.LookupByKey)
                .JoinByDistinctKeyAwait(
                    p => p.KeyB,
                    queryableB.LookupByKey,
                    (existing, @new) => (a: existing, b: @new))
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            Assert.Equal(500, results.Count);
            for (int i = 0; i < 500; i++)
            {
                Assert.NotNull(results[i].value);
                Assert.NotNull(results[i].related.a);
                Assert.NotNull(results[i].related.b);
                Assert.Equal(i, results[i].value.Index);
                Assert.Same(secondaryA[primary[i].KeyA], results[i].related.a);
                Assert.Same(secondaryB[primary[i].KeyB], results[i].related.b);
            }
        }

        [Fact]
        public async Task BatchQueryJoinOverDatastore()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var secondary = await layer.CreateAsync(
                string.Empty,
                Enumerable.Range(0, 100)
                    .ToAsyncEnumerable()
                    .Select(x => new TestModel
                    {
                        forTest = "Secondary-BatchQueryJoinOverDatastore",
                        number1 = x,
                    }),
                null,
                null,
                TestContext.Current.CancellationToken)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            var primary = await layer.CreateAsync(
                string.Empty,
                Enumerable.Range(0, 500)
                    .ToAsyncEnumerable()
                    .Select(x =>
                    {
                        var idx = RandomNumberGenerator.GetInt32(secondary.Count);
                        return new TestModel
                        {
                            forTest = "Primary-BatchQueryJoinOverDatastore",
                            number1 = x,
                            number2 = idx,
                            keyValue = secondary[idx].Key,
                        };
                    }),
                null,
                null,
                TestContext.Current.CancellationToken)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModels = await layer
                    .LoadAsync<TestModel>(
                        string.Empty,
                        primary.Select(x => x.Key).ToAsyncEnumerable(),
                        null,
                        null,
                        TestContext.Current.CancellationToken)
                    .Select(x => x.Value)
                    .WhereNotNull()
                    .ToArrayAsync()
                    .ConfigureAwait(true);
                Assert.Equal(500, loadedModels.Length);
            }).ConfigureAwait(true);

            var results = await layer
                .LoadAsync<TestModel>(
                    string.Empty,
                    primary.Select(x => x.Key!).ToAsyncEnumerable(),
                    null,
                    null,
                    TestContext.Current.CancellationToken)
                .JoinByDistinctKeyAwait(
                    p => p.Value!.keyValue!,
                    (keys, ct) => layer.LoadAsync<TestModel>(
                        string.Empty,
                        keys,
                        null,
                        null,
                        ct))
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            Assert.Equal(500, results.Count);
            for (int i = 0; i < 500; i++)
            {
                Assert.NotNull(results[i].value.Key);
                Assert.NotNull(results[i].value.Value);
                Assert.NotNull(results[i].related);
                Assert.Equal(i, results[i].value.Value!.number1);
                Assert.Equal(
                    secondary[(int)results[i].value.Value!.number2!.Value].Key,
                    results[i].value.Value!.keyValue);
                Assert.Equal(
                    secondary[(int)results[i].value.Value!.number2!.Value].Key,
                    results[i].related!.Key);
                Assert.Equal(
                    secondary[(int)results[i].value.Value!.number2!.Value].number1,
                    results[i].related!.number1);
            }
        }
    }
}
