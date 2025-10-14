namespace Redpoint.CloudFramework.Tests
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.DependencyInjection;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.StringEnum;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    internal class TestStringEnum : StringEnum<TestStringEnum>
    {
        public static readonly StringEnumValue<TestStringEnum> A = Create("a");

        public static readonly StringEnumValue<TestStringEnum> B = Create("b");

        public static readonly StringEnumValue<TestStringEnum> C = Create("c");
    }

    [Kind("testString")]
    internal sealed class TestStringModel : Model<TestStringModel>
    {
        [Type(FieldType.String), Indexed, Default("a")]
        public StringEnumValue<TestStringEnum> enumValue { get; set; } = TestStringEnum.A;

#pragma warning disable CA1861 // Avoid constant arrays as arguments
        [Type(FieldType.StringArray), Indexed, Default(new[] { "a" })]
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        public IReadOnlyList<StringEnumValue<TestStringEnum>> enumArrayValue { get; set; } = new[] { TestStringEnum.A };

#pragma warning disable CA1861 // Avoid constant arrays as arguments
        [Type(FieldType.StringArray), Indexed, Default(new[] { "a" })]
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        public IReadOnlySet<StringEnumValue<TestStringEnum>> enumSetValue { get; set; } = new HashSet<StringEnumValue<TestStringEnum>>(new[] { TestStringEnum.A });

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }
    }

    [Collection("CloudFramework Test")]
    public class StringEnumRepositoryLayerTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public StringEnumRepositoryLayerTests(CloudFrameworkTestEnvironment env)
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

        [Fact]
        public async Task TestCreate()
        {
            var model = new TestStringModel();

            Assert.Equal(TestStringEnum.A, model.enumValue);

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, TestContext.Current.CancellationToken).FirstAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.Equal(TestStringEnum.A, model.enumValue);
        }

        [Fact]
        public async Task TestCreateAndLoad()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            Key modelKey;
            {
                var instant = SystemClock.Instance.GetCurrentInstant();
                var model = new TestStringModel
                {
                    enumValue = TestStringEnum.B,
                    enumArrayValue = new[] { TestStringEnum.B, TestStringEnum.C },
                    enumSetValue = new HashSet<StringEnumValue<TestStringEnum>> { TestStringEnum.B, TestStringEnum.C },
                    timestamp = instant,
                };
                await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, TestContext.Current.CancellationToken).FirstAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                modelKey = model.Key;
            }

            {
                await HandleEventualConsistency(async () =>
                {
                    Assert.NotNull(await layer.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true));
                }).ConfigureAwait(true);
                var model = await layer.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
                Assert.NotNull(model);
                Assert.Equal(TestStringEnum.B, model.enumValue);
                Assert.Equal(new[] { TestStringEnum.B, TestStringEnum.C }, model.enumArrayValue);
                Assert.Equal(new[] { TestStringEnum.B, TestStringEnum.C }, model.enumSetValue);
            }
        }

        [Fact]
        public async Task TestQueryOnEnumValue()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var instant = SystemClock.Instance.GetCurrentInstant();
            {
                var model = new TestStringModel
                {
                    enumValue = TestStringEnum.B,
                    timestamp = instant,
                };
                await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, TestContext.Current.CancellationToken).FirstAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            }

            {
                await HandleEventualConsistency(async () =>
                {
                    Assert.NotNull(await layer.QueryAsync<TestStringModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.enumValue == TestStringEnum.B,
                    null,
                    null,
                    null,
                    null,
                    TestContext.Current.CancellationToken).FirstOrDefaultAsync().ConfigureAwait(true));
                }).ConfigureAwait(true);

                var model = await layer.QueryAsync<TestStringModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.enumValue == TestStringEnum.B,
                    null,
                    null,
                    null,
                    null,
                    TestContext.Current.CancellationToken).FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                Assert.NotNull(model);
            }
        }

        [Fact]
        public async Task TestLoadIntoCache()
        {
            var datastore = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redisCache = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();

            Key modelKey;
            {
                var instant = SystemClock.Instance.GetCurrentInstant();
                var model = new TestStringModel
                {
                    enumValue = TestStringEnum.B,
                    enumArrayValue = new[] { TestStringEnum.B, TestStringEnum.C },
                    timestamp = instant,
                };
                await datastore.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, TestContext.Current.CancellationToken).FirstAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                modelKey = model.Key;
            }

            {
                await HandleEventualConsistency(async () =>
                {
                    Assert.NotNull(await datastore.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true));
                }).ConfigureAwait(true);
                var model = await redisCache.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
                Assert.NotNull(model);
                Assert.Equal(TestStringEnum.B, model.enumValue);
                Assert.Equal(new[] { TestStringEnum.B, TestStringEnum.C }, model.enumArrayValue);
            }
        }

        [Fact]
        public async Task TestRoundtripCache()
        {
            var datastore = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();
            var redisCache = _env.Services.GetRequiredService<IRedisCacheRepositoryLayer>();

            Key modelKey;
            {
                var instant = SystemClock.Instance.GetCurrentInstant();
                var model = new TestStringModel
                {
                    enumValue = TestStringEnum.B,
                    enumArrayValue = new[] { TestStringEnum.B, TestStringEnum.C },
                    enumSetValue = new HashSet<StringEnumValue<TestStringEnum>> { TestStringEnum.B, TestStringEnum.C },
                    timestamp = instant,
                };
                await redisCache.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, TestContext.Current.CancellationToken).FirstAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                modelKey = model.Key;
            }

            {
                await HandleEventualConsistency(async () =>
                {
                    Assert.NotNull(await datastore.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true));
                }).ConfigureAwait(true);
                var model = await redisCache.LoadAsync<TestStringModel>(string.Empty, modelKey, null, null, TestContext.Current.CancellationToken).ConfigureAwait(true);
                Assert.NotNull(model);
                Assert.Equal(TestStringEnum.B, model.enumValue);
                Assert.Equal(new[] { TestStringEnum.B, TestStringEnum.C }, model.enumArrayValue);
                Assert.Equal(new[] { TestStringEnum.B, TestStringEnum.C }, model.enumSetValue);
            }
        }
    }
}
