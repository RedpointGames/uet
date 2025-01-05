using Google.Cloud.Datastore.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Type;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Redpoint.CloudFramework.Models;
using Redpoint.CloudFramework.Repository.Layers;
using Redpoint.CloudFramework.Repository.Metrics;
using StackExchange.Redis;
using System.Text;
using Xunit;
using Xunit.Sdk;
using static Google.Cloud.Datastore.V1.Key.Types;
using Value = Google.Cloud.Datastore.V1.Value;

namespace Redpoint.CloudFramework.Tests
{
    [Collection("CloudFramework Test")]
    public class RedisLayerRepositoryLayerTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public const int DefaultDelayMs = 0;

        public RedisLayerRepositoryLayerTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        private static async Task HandleEventualConsistency(Func<Task> task)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    await task().ConfigureAwait(true);
                    return;
                }
                catch (XunitException)
                {
                    await Task.Delay(100).ConfigureAwait(true);
                }
            }

            await task().ConfigureAwait(true);
        }

        [Kind<TestLoadedEntityMatchesCreatedEntity_Model>("cf_TestLoadedEntityMatchesCreatedEntity")]
        private class TestLoadedEntityMatchesCreatedEntity_Model : RedisTestModel { }

        [Kind<TestLoadedEntityIsInCache_Model>("cf_TestLoadedEntityIsInCache")]
        private class TestLoadedEntityIsInCache_Model : RedisTestModel { }

        [Kind<TestMultipleEntityLoadWorks_Model>("cf_TestMultipleEntityLoadWorks")]
        private class TestMultipleEntityLoadWorks_Model : RedisTestModel { }

        [Kind<TestMultipleEntityLoadWorksWithoutCacheClear_Model>("cf_TestMultipleEntityLoadWorksWithoutCacheClear")]
        private class TestMultipleEntityLoadWorksWithoutCacheClear_Model : RedisTestModel { }

        [Kind<TestUpdatedEntityIsNotInCache_Model>("cf_TestUpdatedEntityIsNotInCache")]
        private class TestUpdatedEntityIsNotInCache_Model : RedisTestModel { }

        [Kind<TestUpsertedEntityIsNotInCache_Model>("cf_TestUpsertedEntityIsNotInCache")]
        private class TestUpsertedEntityIsNotInCache_Model : RedisTestModel { }

        [Kind<TestDeletedEntityIsNotInCache_Model>("cf_TestDeletedEntityIsNotInCache")]
        private class TestDeletedEntityIsNotInCache_Model : RedisTestModel { }

        [Kind<TestCreateThenQuery_Model>("cf_TestCreateThenQuery")]
        private class TestCreateThenQuery_Model : RedisTestModel { }

        [Kind<TestCreateThenQueryThenUpdateThenQuery_Model>("cf_TestCreateThenQueryThenUpdateThenQuery")]
        private class TestCreateThenQueryThenUpdateThenQuery_Model : RedisTestModel { }

        [Kind<TestReaderCountIsSetWhileReading_Model>("cf_TestReaderCountIsSetWhileReading")]
        private class TestReaderCountIsSetWhileReading_Model : RedisTestModel { }

        [Kind<TestTransactionalUpdateInvalidatesQuery_Model>("cf_TestTransactionalUpdateInvalidatesQuery")]
        private class TestTransactionalUpdateInvalidatesQuery_Model : RedisTestModel { }

        [Kind<TestCreateInvalidatesQuery_Model>("cf_TestCreateInvalidatesQuery")]
        private class TestCreateInvalidatesQuery_Model : RedisTestModel { }

        [Kind<TestUpdateWithNoOriginalDataDoesNotCrash_Model>("cf_TestUpdateWithNoOriginalDataDoesNotCrash")]
        private class TestUpdateWithNoOriginalDataDoesNotCrash_Model : RedisTestModel { }

        [Kind<TestUpdateInvalidatesRelevantQuery_Model>("cf_TestUpdateInvalidatesRelevantQuery")]
        private class TestUpdateInvalidatesRelevantQuery_Model : RedisTestModel { }

        [Kind<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>("cf_TestUpdateDoesNotInvalidateIrrelevantQuery")]
        private class TestUpdateDoesNotInvalidateIrrelevantQuery_Model : RedisTestModel { }

        [Kind<TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model>("cf_TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit")]
        private class TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model : RedisTestModel { }

        [Kind<TestTransactionalUpdateFromNull_Model>("cf_TestTransactionalUpdateFromNull")]
        private class TestTransactionalUpdateFromNull_Model : RedisTestModel { }

        [Kind<TestNonTransactionalUpdateFromNull_Model>("cf_TestNonTransactionalUpdateFromNull")]
        private class TestNonTransactionalUpdateFromNull_Model : RedisTestModel { }

        [Kind<TestQueryOrdering_Model>("cf_TestQueryOrdering")]
        private class TestQueryOrdering_Model : RedisTestModel { }

        [Kind<TestQueryEverything_Model>("cf_TestQueryEverything")]
        private class TestQueryEverything_Model : RedisTestModel { }

        [Kind<TestDeletedEntityIsNotInCachedQueryEverything_Model>("cf_TestDeletedEntityIsNotInCachedQueryEverything")]
        private class TestDeletedEntityIsNotInCachedQueryEverything_Model : RedisTestModel { }

        [Fact]
        public async Task TestLoadedEntityMatchesCreatedEntity()
        {
            var model = new TestLoadedEntityMatchesCreatedEntity_Model
            {
                forTest = "TestLoadedEntityMatchesCreatedEntity",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestLoadedEntityMatchesCreatedEntity_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestLoadedEntityMatchesCreatedEntity_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            var metrics = new RepositoryOperationMetrics();
            var loadedModel = await layer.LoadAsync<TestLoadedEntityMatchesCreatedEntity_Model>(string.Empty, model.Key, null, metrics, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(loadedModel!.Key, model.Key);
            Assert.Equal(loadedModel.forTest, model.forTest);
            Assert.Equal(loadedModel.string1, model.string1);
            Assert.Equal(loadedModel.number1, model.number1);
            Assert.Equal(loadedModel.number2, model.number2);
            // NOTE: We can't compare the timestamp field, since Datastore doesn't have
            // as much resolution as C# or the Redis caching layer.

            Assert.True(metrics.CacheDidRead);
        }

        private string SerializePathElement(PathElement pe)
        {
            var kind = pe.Kind.Contains('-', StringComparison.Ordinal) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(pe.Kind)) : pe.Kind;
            if (pe.IdTypeCase == PathElement.IdTypeOneofCase.None)
            {
                return $"{kind}-none";
            }
            else if (pe.IdTypeCase == PathElement.IdTypeOneofCase.Id)
            {
                return $"{kind}-id-{pe.Id}";
            }
            else if (pe.IdTypeCase == PathElement.IdTypeOneofCase.Name)
            {
                return $"{kind}-name-{Convert.ToBase64String(Encoding.UTF8.GetBytes(pe.Name))}";
            }
            throw new NotImplementedException();
        }

        private string GetSimpleCacheKey(Key key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.PartitionId == null) throw new ArgumentNullException("key.PartitionId");
            if (key.PartitionId.ProjectId == null) throw new ArgumentNullException("key.PartitionId.ProjectId");
            if (key.PartitionId.NamespaceId == null) throw new ArgumentNullException("key.PartitionId.NamespaceId");
            if (key.Path == null) throw new ArgumentNullException("key.Path");
            return $"KEY:{key.PartitionId.ProjectId}/{key.PartitionId.NamespaceId}:{string.Join(":", key.Path.Select(SerializePathElement))}";
        }

        [Fact]
        public async Task TestLoadedEntityIsInCache()
        {
            var model = new TestLoadedEntityIsInCache_Model
            {
                forTest = "TestLoadedEntityIsInCache",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestLoadedEntityIsInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestLoadedEntityIsInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            var value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.True(value.HasValue);

            // The format of the cached value is not stable, so we don't test the contents. We just care that
            // it's in the cache.
        }

        [Fact]
        public async Task TestMultipleEntityLoadWorks()
        {
            var models = new[]
            {
                new TestMultipleEntityLoadWorks_Model
                {
                    forTest = "TestMultipleEntityLoadWorks",
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestMultipleEntityLoadWorks_Model
                {
                    forTest = "TestMultipleEntityLoadWorks",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 21,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestMultipleEntityLoadWorks_Model>(string.Empty, models[0].Key, null, null, CancellationToken.None).ConfigureAwait(true));
                Assert.NotNull(await directLayer.LoadAsync<TestMultipleEntityLoadWorks_Model>(string.Empty, models[1].Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestMultipleEntityLoadWorks_Model>(string.Empty, models[0].Key, null, null, CancellationToken.None).ConfigureAwait(true));
            Assert.NotNull(await layer.LoadAsync<TestMultipleEntityLoadWorks_Model>(string.Empty, models[1].Key, null, null, CancellationToken.None).ConfigureAwait(true));

            await redis.KeyDeleteAsync(GetSimpleCacheKey(models[0].Key)).ConfigureAwait(true);
            await redis.KeyDeleteAsync(GetSimpleCacheKey(models[1].Key)).ConfigureAwait(true);

            for (int i = 0; i < 2; i++)
            {
                var loadedModels = await layer.LoadAsync<TestMultipleEntityLoadWorks_Model>(string.Empty, models.Select(x => x.Key).ToAsyncEnumerable(), null, null, CancellationToken.None).ToDictionaryAsync(k => k.Key, v => v.Value).ConfigureAwait(true);

                Assert.True(loadedModels.ContainsKey(models[0].Key));
                Assert.True(loadedModels.ContainsKey(models[1].Key));

                Assert.Equal(loadedModels[models[0].Key]!.Key, models[0].Key);
                Assert.Equal(loadedModels[models[0].Key]!.forTest, models[0].forTest);
                Assert.Equal(loadedModels[models[0].Key]!.string1, models[0].string1);
                Assert.Equal(loadedModels[models[0].Key]!.number1, models[0].number1);
                Assert.Equal(loadedModels[models[0].Key]!.number2, models[0].number2);

                Assert.Equal(loadedModels[models[1].Key]!.Key, models[1].Key);
                Assert.Equal(loadedModels[models[1].Key]!.forTest, models[1].forTest);
                Assert.Equal(loadedModels[models[1].Key]!.string1, models[1].string1);
                Assert.Equal(loadedModels[models[1].Key]!.number1, models[1].number1);
                Assert.Equal(loadedModels[models[1].Key]!.number2, models[1].number2);

                Assert.True((await redis.StringGetAsync(GetSimpleCacheKey(models[0].Key)).ConfigureAwait(true)).HasValue);
                Assert.True((await redis.StringGetAsync(GetSimpleCacheKey(models[1].Key)).ConfigureAwait(true)).HasValue);
            }
        }

        [Fact]
        public async Task TestMultipleEntityLoadWorksWithoutCacheClear()
        {
            var models = new[]
            {
                new TestMultipleEntityLoadWorksWithoutCacheClear_Model
                {
                    forTest = "TestMultipleEntityLoadWorks",
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestMultipleEntityLoadWorksWithoutCacheClear_Model
                {
                    forTest = "TestMultipleEntityLoadWorks",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 21,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestMultipleEntityLoadWorksWithoutCacheClear_Model>(string.Empty, models[0].Key, null, null, CancellationToken.None).ConfigureAwait(true));
                Assert.NotNull(await directLayer.LoadAsync<TestMultipleEntityLoadWorksWithoutCacheClear_Model>(string.Empty, models[1].Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestMultipleEntityLoadWorksWithoutCacheClear_Model>(string.Empty, models[0].Key, null, null, CancellationToken.None).ConfigureAwait(true));
            Assert.NotNull(await layer.LoadAsync<TestMultipleEntityLoadWorksWithoutCacheClear_Model>(string.Empty, models[1].Key, null, null, CancellationToken.None).ConfigureAwait(true));

            await redis.KeyDeleteAsync(GetSimpleCacheKey(models[0].Key)).ConfigureAwait(true);
            await redis.KeyDeleteAsync(GetSimpleCacheKey(models[1].Key)).ConfigureAwait(true);

            for (int i = 0; i < 2; i++)
            {
                var loadedModels = await layer.LoadAsync<TestMultipleEntityLoadWorksWithoutCacheClear_Model>(string.Empty, models.Select(x => x.Key).ToAsyncEnumerable(), null, null, CancellationToken.None).ToDictionaryAsync(k => k.Key, v => v.Value).ConfigureAwait(true);

                Assert.True(loadedModels.ContainsKey(models[0].Key));
                Assert.True(loadedModels.ContainsKey(models[1].Key));

                Assert.Equal(loadedModels[models[0].Key]!.Key, models[0].Key);
                Assert.Equal(loadedModels[models[0].Key]!.forTest, models[0].forTest);
                Assert.Equal(loadedModels[models[0].Key]!.string1, models[0].string1);
                Assert.Equal(loadedModels[models[0].Key]!.number1, models[0].number1);
                Assert.Equal(loadedModels[models[0].Key]!.number2, models[0].number2);

                Assert.Equal(loadedModels[models[1].Key]!.Key, models[1].Key);
                Assert.Equal(loadedModels[models[1].Key]!.forTest, models[1].forTest);
                Assert.Equal(loadedModels[models[1].Key]!.string1, models[1].string1);
                Assert.Equal(loadedModels[models[1].Key]!.number1, models[1].number1);
                Assert.Equal(loadedModels[models[1].Key]!.number2, models[1].number2);

                Assert.True((await redis.StringGetAsync(GetSimpleCacheKey(models[0].Key)).ConfigureAwait(true)).HasValue);
                Assert.True((await redis.StringGetAsync(GetSimpleCacheKey(models[1].Key)).ConfigureAwait(true)).HasValue);
            }
        }

        [Fact]
        public async Task TestUpdatedEntityIsNotInCache()
        {
            var model = new TestUpdatedEntityIsNotInCache_Model
            {
                forTest = "TestUpdatedEntityIsNotInCache",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestUpdatedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestUpdatedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.True(value.HasValue);

            model.string1 = "test2";
            await layer.UpdateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);
        }

        [Fact]
        public async Task TestUpsertedEntityIsNotInCache()
        {
            var model = new TestUpsertedEntityIsNotInCache_Model
            {
                forTest = "TestUpsertedEntityIsNotInCache",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestUpsertedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestUpsertedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.True(value.HasValue);

            model.string1 = "test2";
            await layer.UpsertAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);
        }

        [Fact]
        public async Task TestDeletedEntityIsNotInCache()
        {
            var model = new TestDeletedEntityIsNotInCache_Model
            {
                forTest = "TestDeletedEntityIsNotInCache",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestDeletedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestDeletedEntityIsNotInCache_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.True(value.HasValue);

            await layer.DeleteAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);
        }

        [Fact]
        public async Task TestCreateThenQuery()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestCreateThenQuery_Model
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestCreateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQuery",
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQuery",
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);
            }

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQuery",
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.False(metrics.CacheDidWrite);
                Assert.True(metrics.CacheDidRead);
            }
        }

        [Fact]
        public async Task TestCreateThenQueryThenUpdateThenQuery()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestCreateThenQueryThenUpdateThenQuery_Model
            {
                forTest = "TestCreateThenQueryThenUpdateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestCreateThenQueryThenUpdateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQueryThenUpdateThenQuery",
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateThenQueryThenUpdateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQueryThenUpdateThenQuery",
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestCreateThenQueryThenUpdateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQueryThenUpdateThenQuery",
                    null,
                    1,
                    null,
                    queryMetrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");
            }

            {
                var metrics = new RepositoryOperationMetrics();
                model.string1 = "test2";
                await layer.UpdateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.True(metrics.CacheQueriesFlushed >= 1);

                Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} exists");
            }

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateThenQueryThenUpdateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQueryThenUpdateThenQuery",
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateThenQueryThenUpdateThenQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQueryThenUpdateThenQuery",
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.False(metrics.CacheDidWrite);
                Assert.True(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }
        }

        [Fact]
        public async Task TestReaderCountIsSetWhileReading()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestReaderCountIsSetWhileReading_Model
                {
                    forTest = "TestReaderCountIsSetWhileReading",
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestReaderCountIsSetWhileReading_Model
                {
                    forTest = "TestReaderCountIsSetWhileReading",
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(2, await directLayer.QueryAsync<TestReaderCountIsSetWhileReading_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestReaderCountIsSetWhileReading",
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            await layer.QueryAsync<TestReaderCountIsSetWhileReading_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == "TestReaderCountIsSetWhileReading",
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);

            var metrics = new RepositoryOperationMetrics();
            var enumerator = layer.QueryAsync<TestReaderCountIsSetWhileReading_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == "TestReaderCountIsSetWhileReading",
                null,
                null,
                null,
                metrics,
                CancellationToken.None).GetAsyncEnumerator();

            Assert.True((await cache.StringGetAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true)).IsNull);
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                if (enumerator.Current != null)
                {
                    Assert.Equal("1", await cache.StringGetAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true));
                }
            }
            await enumerator.DisposeAsync().ConfigureAwait(true);
            Assert.True((await cache.StringGetAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true)).IsNull);
        }

        [Fact]
        public async Task TestTransactionalUpdateInvalidatesQuery()
        {
            const string name = "TestTransactionalUpdateInvalidatesQuery";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestTransactionalUpdateInvalidatesQuery_Model
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestTransactionalUpdateInvalidatesQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            await layer.QueryAsync<TestTransactionalUpdateInvalidatesQuery_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);

            var metrics = new RepositoryOperationMetrics();
            var entity = await layer.QueryAsync<TestTransactionalUpdateInvalidatesQuery_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                metrics,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

            Assert.True(metrics.CacheDidRead);

            var updateMetrics = new RepositoryOperationMetrics();
            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);
            models[0].string1 = "test2";
            await layer.UpdateAsync(string.Empty, models.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);
            await layer.CommitAsync(string.Empty, transaction, updateMetrics, CancellationToken.None).ConfigureAwait(true);

            Assert.True(updateMetrics.CacheQueriesFlushed > 1);

            Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} exists");
            Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
            Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
            Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} exists");

            metrics = new RepositoryOperationMetrics();
            entity = await layer.QueryAsync<TestTransactionalUpdateInvalidatesQuery_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                metrics,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

            Assert.NotNull(entity);
            Assert.False(metrics.CacheDidRead);
            Assert.True(metrics.CacheDidWrite);
            Assert.Equal("test2", entity.string1);
        }

        [Fact]
        public async Task TestCreateInvalidatesQuery()
        {
            const string name = "TestCreateInvalidatesQuery";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestCreateInvalidatesQuery_Model
            {
                forTest = name,
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestCreateInvalidatesQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestCreateInvalidatesQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    1,
                    null,
                    metrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestCreateInvalidatesQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    1,
                    null,
                    queryMetrics,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");
            }

            {
                var newModel = new TestCreateInvalidatesQuery_Model
                {
                    forTest = name,
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                };

                var metrics = new RepositoryOperationMetrics();
                await layer.CreateAsync(string.Empty, new[] { newModel }.ToAsyncEnumerable(), null, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} exists");
            }
        }

        [Fact]
        public async Task TestUpdateWithNoOriginalDataDoesNotCrash()
        {
            const string name = "TestUpdateWithNoOriginalDataDoesNotCrash";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestUpdateWithNoOriginalDataDoesNotCrash_Model
                {
                    forTest = name,
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestUpdateWithNoOriginalDataDoesNotCrash_Model
                {
                    forTest = name,
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(2, await directLayer.QueryAsync<TestUpdateWithNoOriginalDataDoesNotCrash_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestUpdateWithNoOriginalDataDoesNotCrash_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    metrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestUpdateWithNoOriginalDataDoesNotCrash_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    queryMetrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");
            }

            {
                models[0].string1 = "test3";

                var metrics = new RepositoryOperationMetrics();
                await layer.UpdateAsync(string.Empty, new[] { models[0] }.ToAsyncEnumerable(), null, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} exists");
            }
        }

        [Fact]
        public async Task TestUpdateInvalidatesRelevantQuery()
        {
            const string name = "TestUpdateInvalidatesRelevantQuery";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestUpdateInvalidatesRelevantQuery_Model
                {
                    forTest = name,
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestUpdateInvalidatesRelevantQuery_Model
                {
                    forTest = name,
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(2, await directLayer.QueryAsync<TestUpdateInvalidatesRelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestUpdateInvalidatesRelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    metrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestUpdateInvalidatesRelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    queryMetrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");

                models[0] = result[0];
            }

            {
                models[0].string1 = "test3";

                var metrics = new RepositoryOperationMetrics();
                await layer.UpdateAsync(string.Empty, new[] { models[0] }.ToAsyncEnumerable(), null, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} exists");
            }
        }

        [Fact]
        public async Task TestUpdateDoesNotInvalidateIrrelevantQuery()
        {
            const string name = "TestUpdateDoesNotInvalidateIrrelevantQuery";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestUpdateDoesNotInvalidateIrrelevantQuery_Model
                {
                    forTest = name,
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestUpdateDoesNotInvalidateIrrelevantQuery_Model
                {
                    forTest = name,
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(2, await directLayer.QueryAsync<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    metrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    queryMetrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");
            }

            models[1] = (await directLayer.LoadAsync<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>(string.Empty, models[1].Key, null, null, CancellationToken.None).ConfigureAwait(true))!;

            {
                models[1].number1 = 200;

                var metrics = new RepositoryOperationMetrics();
                await layer.UpdateAsync(string.Empty, new[] { models[1] }.ToAsyncEnumerable(), null, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");
            }
        }

        [Fact]
        public async Task TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit()
        {
            const string name = "TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model
                {
                    forTest = name,
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model
                {
                    forTest = name,
                    string1 = "test2",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(2, await directLayer.QueryAsync<TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            {
                var metrics = new RepositoryOperationMetrics();
                var result = await layer.QueryAsync<TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    metrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.True(metrics.CacheDidWrite);
                Assert.False(metrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{metrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{metrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{metrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{metrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{metrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{metrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{metrics.CacheHash} is persistent (should be TTL)");
            }

            var queryMetrics = new RepositoryOperationMetrics();
            {
                var result = await layer.QueryAsync<TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name && x.string1 == "test1",
                    null,
                    null,
                    null,
                    queryMetrics,
                    CancellationToken.None).ToListAsync().ConfigureAwait(true);

                Assert.Single(result);
                Assert.False(queryMetrics.CacheDidWrite);
                Assert.True(queryMetrics.CacheDidRead);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYCACHE:{queryMetrics.CacheHash} is persistent (should be TTL)");
                Assert.False(await cache.KeyTimeToLiveAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true) == null, $"QUERYDATA:{queryMetrics.CacheHash} is persistent (should be TTL)");

                models[0] = result[0];
            }

            {
                models[0].string1 = "test3";

                var metrics = new RepositoryOperationMetrics();

                var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);
                await layer.UpdateAsync(string.Empty, new[] { models[0] }.ToAsyncEnumerable(), transaction, metrics, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                Assert.True(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} does not exist");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.True(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} does not exist");

                await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

                Assert.False(await cache.KeyExistsAsync($"QUERYCACHE:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYCACHE:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYRC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYRC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYWC:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYWC:{queryMetrics.CacheHash} exists");
                Assert.False(await cache.KeyExistsAsync($"QUERYDATA:{queryMetrics.CacheHash}").ConfigureAwait(true), $"QUERYDATA:{queryMetrics.CacheHash} exists");
            }
        }

        [Fact]
        public async Task TestCreateAndLoadWithEmbeddedEntity()
        {
            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();

            var subentity1 = new Entity();
            subentity1.Key = null;
            subentity1["a"] = "hello";

            var subentity2 = new Entity();
            subentity2.Key = null;
            subentity2["a"] = "world";

            var subentity3 = new Entity();
            subentity3.Key = null;
            subentity3["a"] = "blah";

            var entity = new Entity();
            entity.Key = null;
            entity["null"] = Value.ForNull();
            entity["string"] = "test";
            entity["integer"] = 5;
            entity["double"] = 5.0;
            entity["array"] = new[]
            {
                subentity1,
                subentity2,
            };
            entity["arrayString"] = new[]
            {
                "hello",
                "world"
            };
            entity["blob"] = ByteString.CopyFromUtf8("test");
            entity["entity"] = subentity3;
            entity["geopoint"] = new LatLng { Latitude = 20, Longitude = 40 };
            entity["key"] = (await layer.GetKeyFactoryAsync<EmbeddedEntityModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true)).CreateKey(1);
            entity["timestamp"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);

            var model = new EmbeddedEntityModel
            {
                forTest = "TestCreateAndLoadWithEmbeddedEntityRedis",
                timestamp = SystemClock.Instance.GetCurrentInstant(),
                entity = entity,
            };

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<EmbeddedEntityModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var loadedModel = await layer.LoadAsync<EmbeddedEntityModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

            Assert.NotEqual(loadedModel, model);
            Assert.NotNull(loadedModel!.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                RedisLayerRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }
        }

        private static void AssertProperty(Entity entity, string name, Value compareWith)
        {
            Assert.True(entity.Properties.ContainsKey(name));
            Assert.NotNull(entity[name]);
            if (compareWith.ValueTypeCase == Value.ValueTypeOneofCase.TimestampValue)
            {
                Assert.Equal(compareWith.ValueTypeCase, entity[name].ValueTypeCase);
                Assert.Equal(compareWith.TimestampValue.Seconds, entity[name].TimestampValue.Seconds);
            }
            else
            {
                Assert.Equal(compareWith, entity[name]);
            }
        }

        [Fact]
        public async Task TestCreateAndQueryWithEmbeddedEntity()
        {
            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();

            var subentity1 = new Entity();
            subentity1.Key = null;
            subentity1["a"] = "hello";

            var subentity2 = new Entity();
            subentity2.Key = null;
            subentity2["a"] = "world";

            var subentity3 = new Entity();
            subentity3.Key = null;
            subentity3["a"] = "blah";

            var entity = new Entity();
            entity.Key = null;
            entity["null"] = Value.ForNull();
            entity["string"] = "test";
            entity["integer"] = 5;
            entity["double"] = 5.0;
            entity["array"] = new[]
            {
                subentity1,
                subentity2,
            };
            entity["arrayString"] = new[]
            {
                "hello",
                "world"
            };
            entity["blob"] = ByteString.CopyFromUtf8("test");
            entity["entity"] = subentity3;
            entity["geopoint"] = new LatLng { Latitude = 20, Longitude = 40 };
            entity["key"] = (await layer.GetKeyFactoryAsync<EmbeddedEntityModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true)).CreateKey(1);
            entity["timestamp"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);

            const string name = "TestCreateAndQueryWithEmbeddedEntityRedis";
            var timestamp = SystemClock.Instance.GetCurrentInstant();

            var model = new EmbeddedEntityModel
            {
                forTest = name,
                timestamp = timestamp,
                entity = entity,
            };

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<EmbeddedEntityModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var loadedModel = await layer.QueryAsync<EmbeddedEntityModel>(
                string.Empty,
                x =>
                    x.forTest == name &&
                    x.timestamp == timestamp &&
                    x.entity!["string"].StringValue == "test",
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
            Assert.NotNull(loadedModel);

            Assert.NotEqual(loadedModel, model);
            Assert.NotNull(loadedModel!.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                RedisLayerRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }

            loadedModel = await layer.QueryAsync<EmbeddedEntityModel>(
                string.Empty,
                x =>
                    x.forTest == name &&
                    x.timestamp == timestamp &&
                    x.entity!["string"].StringValue == "not_found",
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
            Assert.Null(loadedModel);

            loadedModel = await layer.QueryAsync<EmbeddedEntityModel>(
                string.Empty,
                x =>
                    x.forTest == name &&
                    x.timestamp == timestamp &&
                    x.entity!["arrayString"].StringValue == "hello",
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
            Assert.NotNull(loadedModel);

            Assert.NotEqual(loadedModel, model);
            Assert.NotNull(loadedModel.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                RedisLayerRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }

            loadedModel = await layer.QueryAsync<EmbeddedEntityModel>(
                string.Empty,
                x =>
                    x.forTest == name &&
                    x.timestamp == timestamp &&
                    x.entity!["array"].EntityValue["a"].StringValue == "world",
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
            Assert.NotNull(loadedModel);

            Assert.NotEqual(loadedModel, model);
            Assert.NotNull(loadedModel.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                RedisLayerRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }
        }

        [Fact]
        public async Task TestTransactionalUpdateFromNull()
        {
            const string name = "TestTransactionalUpdateFromNull";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestTransactionalUpdateFromNull_Model
                {
                    forTest = name,
                    string1 = null,
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestTransactionalUpdateFromNull_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var entity = await layer.QueryAsync<TestTransactionalUpdateFromNull_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);
            Assert.NotNull(entity);
            entity.string1 = "test2";
            await layer.UpdateAsync(string.Empty, new[] { entity }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);
            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestNonTransactionalUpdateFromNull()
        {
            const string name = "TestNonTransactionalUpdateFromNull";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestNonTransactionalUpdateFromNull_Model
                {
                    forTest = name,
                    string1 = null,
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var cache = (_env.Services.GetRequiredService<IConnectionMultiplexer>()).GetDatabase();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await directLayer.QueryAsync<TestNonTransactionalUpdateFromNull_Model>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var entity = await layer.QueryAsync<TestNonTransactionalUpdateFromNull_Model>(
                string.Empty,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
            Assert.NotNull(entity);

            entity.string1 = "test2";
            await layer.UpdateAsync(string.Empty, new[] { entity }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToListAsync().ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryOrdering()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestQueryOrdering_Model
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestQueryOrdering_Model
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test2",
                    number1 = 10,
                    number2 = 21,
                    timestamp = instant,
                },
                new TestQueryOrdering_Model
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test3",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestQueryOrdering_Model>(
                    string.Empty,
                    x =>
                        x.timestamp == instant &&
                        x.forTest == "TestQueryOrdering",
                    x => x.number1 < x.number1 | x.number2 > x.number2,
                    3,
                    null,
                    null,
                    CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

                Assert.Equal(3, result.Length);
                Assert.Equal("test2", result[0].string1);
                Assert.Equal("test1", result[1].string1);
                Assert.Equal("test3", result[2].string1);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryEverything()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestQueryEverything_Model
                {
                    forTest = "TestQueryEverything",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestQueryEverything_Model
                {
                    forTest = "TestQueryEverything",
                    string1 = "test2",
                    number1 = 10,
                    number2 = 21,
                    timestamp = instant,
                },
                new TestQueryEverything_Model
                {
                    forTest = "TestQueryEverything",
                    string1 = "test3",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await directLayer.QueryAsync<TestQueryEverything_Model>(
                    string.Empty,
                    x => true,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
                Assert.Equal(3, result.Count(x => x.forTest == "TestQueryEverything" && Math.Abs((x.timestamp - instant)?.TotalSeconds ?? double.MaxValue) < 0.5));
            }).ConfigureAwait(true);

            var result = await layer.QueryAsync<TestQueryEverything_Model>(
                string.Empty,
                x => true,
                null,
                null,
                null,
                null,
                CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            Assert.Equal(3, result.Count(x => x.forTest == "TestQueryEverything" && Math.Abs((x.timestamp - instant)?.TotalSeconds ?? double.MaxValue) < 0.5));
        }

        [Fact]
        public async Task TestDeletedEntityIsNotInCachedQueryEverything()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestDeletedEntityIsNotInCachedQueryEverything_Model
            {
                forTest = "TestDeletedEntityIsNotInCachedQueryEverything",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();
            var directLayer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redis = _env.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);

            await RedisLayerRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await directLayer.LoadAsync<TestDeletedEntityIsNotInCachedQueryEverything_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var countedEntities = await layer.QueryAsync<TestDeletedEntityIsNotInCachedQueryEverything_Model>(
                string.Empty,
                x => true,
                null,
                null,
                null,
                null,
                CancellationToken.None)
                .CountAsync(x => x.forTest == "TestDeletedEntityIsNotInCachedQueryEverything" && Math.Abs((x.timestamp - instant)?.TotalSeconds ?? double.MaxValue) < 0.5).ConfigureAwait(true);
            Assert.Equal(1, countedEntities);

            Assert.NotNull(await layer.LoadAsync<TestDeletedEntityIsNotInCachedQueryEverything_Model>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.True(value.HasValue);

            await layer.DeleteAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);

            value = await redis.StringGetAsync(GetSimpleCacheKey(model.Key)).ConfigureAwait(true);
            Assert.False(value.HasValue);

            countedEntities = await layer.QueryAsync<TestDeletedEntityIsNotInCachedQueryEverything_Model>(
                string.Empty,
                x => true,
                null,
                null,
                null,
                null,
                CancellationToken.None)
                .CountAsync(x => x.forTest == "TestDeletedEntityIsNotInCachedQueryEverything" && (x.timestamp - instant)?.TotalSeconds < 0.5).ConfigureAwait(true);
            Assert.Equal(0, countedEntities);
        }
    }
}
