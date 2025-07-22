using Google.Cloud.Datastore.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Type;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Redpoint.CloudFramework.Repository;
using Redpoint.CloudFramework.Repository.Layers;
using Redpoint.CloudFramework.Tests.Models;
using Xunit;
using Xunit.Sdk;
using static Google.Cloud.Datastore.V1.Key.Types;
using Value = Google.Cloud.Datastore.V1.Value;

namespace Redpoint.CloudFramework.Tests
{
    [Collection("CloudFramework Test")]
    public class DatastoreRepositoryLayerTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public const int DefaultDelayMs = 0;

        public DatastoreRepositoryLayerTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        internal static async Task HandleEventualConsistency(Func<Task> task)
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
            var model = new TestModel
            {
                forTest = "TestCreate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);
        }

        [Fact]
        public async Task TestCreateFiresEntityModificationEvent()
        {
            var model = new TestModel
            {
                forTest = "TestCreateFiresEntityModificationEvent",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            Key[]? modifiedKeys = null;
            layer.OnNonTransactionalEntitiesModified.Add((ev, cancellationToken) =>
            {
                modifiedKeys = ev.Keys;
                return Task.CompletedTask;
            });

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);
            Assert.NotNull(modifiedKeys);
            Assert.Contains(model.Key, modifiedKeys);
        }

        [Fact]
        public async Task TestCreateNullThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.CreateAsync(string.Empty, new TestModel[] { null! }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpsertNullThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.UpsertAsync(string.Empty, new TestModel[] { null! }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpdateNullThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.UpdateAsync(string.Empty, new TestModel[] { null! }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestDeleteNullThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.DeleteAsync(string.Empty, new TestModel[] { null! }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestDeleteSameKeyDoesNotThrow()
        {
            var model = new TestModel
            {
                forTest = "TestDeleteSameKeyDoesNotThrow",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            // This should not throw.
            await layer.DeleteAsync(string.Empty, new[] { model, model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);
        }

        [Fact]
        public void TestIncorrectEventAssignmentFiresException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                layer.OnNonTransactionalEntitiesModified.Add(null!);
            });
        }

        [Fact]
        public async Task TestCreateMultiple()
        {
            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            Assert.All(returnedModels, x =>
            {
                Assert.NotNull(x.Key);
            });
            Assert.Equal(models, returnedModels);
        }

        [Fact]
        public async Task TestCreateThenQuery()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestModel
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestCreateThenQuery",
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestCreateFailsWithDuplicate()
        {
            var firstModel = new TestModel
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var secondModel = new TestModel
                {
                    Key = firstModel.Key,
                    forTest = "TestCreateThenQuery",
                    string1 = "test1",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                };

                var ex = await Assert.ThrowsAsync<RpcException>(async () =>
                {
                    await layer.CreateAsync(string.Empty, new[] { secondModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
                }).ConfigureAwait(true);
                Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpsert()
        {
            var firstModel = new TestModel
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModel = await layer.UpsertAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.Equal(firstModel, returnedModel);
            Assert.NotNull(firstModel.Key);
            Assert.NotNull(returnedModel.Key);
        }

        [Fact]
        public async Task TestUpsertDuplicate()
        {
            var firstModel = new TestModel
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var secondModel = new TestModel
                {
                    Key = firstModel.Key,
                    forTest = "TestCreateThenQuery",
                    string1 = "test1",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                };

                await layer.UpsertAsync(string.Empty, new[] { secondModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpsertDuplicateThenQuery()
        {
            var firstModel = new TestModel
            {
                forTest = "TestCreateThenQuery",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var secondInstant = SystemClock.Instance.GetCurrentInstant();

            var secondModel = new TestModel
            {
                Key = firstModel.Key,
                forTest = "TestCreateThenQuery",
                string1 = "test1",
                number1 = 11,
                number2 = 22,
                timestamp = secondInstant,
            };

            await layer.UpsertAsync(string.Empty, new[] { secondModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                string.Empty,
                x =>
                    x.forTest == "TestCreateThenQuery" &&
                    x.string1 == "test1" &&
                    x.number1 == 11 &&
                    x.number2 == 22 &&
                    x.timestamp == secondInstant,
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, secondModel.Key);
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(9, 2)]
        [InlineData(10, 1)]
        [InlineData(11, 0)]
        [InlineData(12, 0)]
        public async Task TestQueryGreaterThan(int threshold, int count)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(count, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x =>
                        x.forTest == "TestQueryGreaterThan" &&
                        x.timestamp == instant &&
                        x.number1 > threshold,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(9, 2)]
        [InlineData(10, 2)]
        [InlineData(11, 1)]
        [InlineData(12, 0)]
        public async Task TestQueryGreaterThanOrEqualTo(int threshold, int count)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(count, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x =>
                        x.forTest == "TestQueryGreaterThan" &&
                        x.timestamp == instant &&
                        x.number1 >= threshold,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(9, 0)]
        [InlineData(10, 0)]
        [InlineData(11, 1)]
        [InlineData(12, 2)]
        public async Task TestQueryLessThan(int threshold, int count)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(count, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x =>
                        x.forTest == "TestQueryGreaterThan" &&
                        x.timestamp == instant &&
                        x.number1 < threshold,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(9, 0)]
        [InlineData(10, 1)]
        [InlineData(11, 2)]
        [InlineData(12, 2)]
        public async Task TestQueryLessThanOrEqualTo(int threshold, int count)
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryGreaterThan",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(count, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x =>
                        x.forTest == "TestQueryGreaterThan" &&
                        x.timestamp == instant &&
                        x.number1 <= threshold,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryHasAncestor()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var factory = await layer.GetKeyFactoryAsync<TestModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true);
            var parentKey = await layer.AllocateKeyAsync<TestModel>(string.Empty, null, null, CancellationToken.None).ConfigureAwait(true);
            var childKey = parentKey.WithElement(new TestModel().GetKind(), "child");

            var models = new[]
            {
                new TestModel
                {
                    Key = parentKey,
                    forTest = "TestQueryHasAncestor",
                    string1 = "parent",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    Key = childKey,
                    forTest = "TestQueryHasAncestor",
                    string1 = "child",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                }
            };

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x =>
                        x.Key.HasAncestor(parentKey) &&
                        x.forTest == "TestQueryHasAncestor" &&
                        x.timestamp == instant &&
                        x.number1 == 11 &&
                        x.number2 == 22,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryNull()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model = new TestModel
            {
                forTest = "TestQueryNull",
                string1 = null,
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestQueryNull" && x.string1 == null,
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model.Key);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryEverything()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            // Just make sure this doesn't throw an exception; there's no state we can
            // check against when doing an "everything" query.
            await layer.QueryAsync<TestModel>(
                string.Empty,
                x => true,
                null,
                1,
                null,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);
        }

        [Fact]
        public async Task TestInvalidQueriesAreCaught()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.Key == null,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.keyValue!.HasAncestor(null),
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => ((TestModel)null!).Key.HasAncestor(null),
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.keyValue!.IsRoot,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.keyValue!.Equals(null),
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => ((Key)null!).HasAncestor(null),
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.number1 != 20,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => ((TestModel)null!).number1 == 20,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => false,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => false,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.untracked == null,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.GetKind() == null,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<BadModel>(
                    string.Empty,
                    x => x.badField == new object(),
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    null!,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.string1 == null,
                    x => true,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.string1 == null,
                    x => x.number1 > x.number2,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryOrdering()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test2",
                    number1 = 10,
                    number2 = 21,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = "TestQueryOrdering",
                    string1 = "test3",
                    number1 = 11,
                    number2 = 22,
                    timestamp = instant,
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
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
        public async Task TestTransactionalQuery()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var factory = await layer.GetKeyFactoryAsync<TestModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true);
            var parentKey = await layer.AllocateKeyAsync<TestModel>(string.Empty, null, null, CancellationToken.None).ConfigureAwait(true);
            var childKey = parentKey.WithElement(new PathElement { Kind = new TestModel().GetKind() });

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var result = await layer.QueryAsync<TestModel>(
                string.Empty,
                x =>
                    x.Key.HasAncestor(parentKey) &&
                    x.timestamp == instant &&
                    x.forTest == "TestTransactionalQuery",
                null,
                null,
                transaction,
                null,
                CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

            Assert.Null(result);

            var model = new TestModel
            {
                Key = childKey,
                forTest = "TestTransactionalQuery",
                timestamp = instant,
            };

            var createdModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

            Assert.NotNull(createdModel);
            Assert.Equal(model, createdModel);
            Assert.NotNull(model.Key);
            Assert.NotNull(createdModel.Key);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestTransactionCreate()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var firstModel = new TestModel
            {
                forTest = "TestTransactionCreate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var returnedModel = await layer.UpsertAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.Equal(firstModel, returnedModel);
            Assert.NotNull(firstModel.Key);
            Assert.NotNull(returnedModel.Key);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestTransactionCreateWithRollback()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var firstModel = new TestModel
            {
                forTest = "TestTransactionCreateWithRollback",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var returnedModel = await layer.UpsertAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.Equal(firstModel, returnedModel);
            Assert.NotNull(firstModel.Key);
            Assert.NotNull(returnedModel.Key);

            await layer.RollbackAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            Assert.Null(await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
        }

        [Fact]
        public async Task TestTransactionUpsert()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var firstModel = new TestModel
            {
                forTest = "TestTransactionUpsert",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var returnedModel = await layer.UpsertAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.Equal(firstModel, returnedModel);
            Assert.NotNull(firstModel.Key);
            Assert.NotNull(returnedModel.Key);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(10, "a")]
        [InlineData(20, "b")]
        public async Task TestTransactionalUpdate(int value, string expectedValue)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var firstModel = new TestModel
            {
                forTest = "TestTransactionalUpdate",
                string1 = "",
                number1 = value,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await layer.CreateAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var loadedModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, transaction, null, CancellationToken.None).ConfigureAwait(true);

            loadedModel!.string1 = loadedModel.number1 == 10 ? "a" : "b";

            await layer.UpdateAsync(string.Empty, new[] { loadedModel }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            var oldModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(oldModel!.string1, string.Empty);
            Assert.Equal(oldModel.number1, value);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var refreshedModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal(refreshedModel!.string1, expectedValue);
                Assert.Equal(refreshedModel.number1, loadedModel.number1);
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData(10, "a")]
        [InlineData(20, "b")]
        public async Task TestTransactionalUpsert(int value, string expectedValue)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var firstModel = new TestModel
            {
                forTest = "TestTransactionalUpdate",
                string1 = "",
                number1 = value,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await layer.CreateAsync(string.Empty, new[] { firstModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            var loadedModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, transaction, null, CancellationToken.None).ConfigureAwait(true);

            loadedModel!.string1 = loadedModel.number1 == 10 ? "a" : "b";

            await layer.UpsertAsync(string.Empty, new[] { loadedModel }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            var oldModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(oldModel!.string1, string.Empty);
            Assert.Equal(oldModel.number1, value);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var refreshedModel = await layer.LoadAsync<TestModel>(string.Empty, firstModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal(refreshedModel!.string1, expectedValue);
                Assert.Equal(refreshedModel.number1, loadedModel.number1);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpdate()
        {
            var model = new TestModel
            {
                forTest = "TestUpdate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            model.string1 = "updated";

            await layer.UpdateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var refreshedModel = await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal("updated", refreshedModel!.string1);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpdateThrowsIfEntityDoesNotExist()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var model = new TestModel
            {
                Key = (await layer.GetKeyFactoryAsync<TestModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true)).CreateKey("nonexistant"),
                forTest = "TestUpdate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await layer.UpdateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            Assert.Equal(StatusCode.NotFound, ex.StatusCode);
        }

        [Fact]
        public async Task TestUpdateDoesNotUpdateCreationTime()
        {
            var model = new TestModel
            {
                forTest = "TestUpdate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var createdDate = model.dateCreatedUtc;
            Assert.NotNull(createdDate);

            await Task.Delay(200).ConfigureAwait(true);

            model.string1 = "updated";

            await layer.UpdateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var refreshedModel = await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal("updated", refreshedModel!.string1);
                Assert.NotNull(createdDate);

                // We compare milliseconds because Datastore has less resolution than C#, so there is
                // some loss at the tick level even under normal operation. We use the Task.Delay above
                // to ensure that if there is misbehaviour, it will be caught.
                Assert.Equal(createdDate!.Value.ToUnixTimeMilliseconds(), refreshedModel.dateCreatedUtc!.Value.ToUnixTimeMilliseconds());
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestUpsertDoesNotUpdateCreationTime()
        {
            var model = new TestModel
            {
                forTest = "TestUpdate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            var createdDate = model.dateCreatedUtc;
            Assert.NotNull(createdDate);

            await Task.Delay(200).ConfigureAwait(true);

            model.string1 = "updated";

            await layer.UpsertAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var refreshedModel = await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal("updated", refreshedModel!.string1);
                Assert.NotNull(createdDate);

                // We compare milliseconds because Datastore has less resolution than C#, so there is
                // some loss at the tick level even under normal operation. We use the Task.Delay above
                // to ensure that if there is misbehaviour, it will be caught.
                Assert.Equal(createdDate!.Value.ToUnixTimeMilliseconds(), refreshedModel.dateCreatedUtc!.Value.ToUnixTimeMilliseconds());
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestCreateThenDelete()
        {
            var model = new TestModel
            {
                forTest = "TestCreate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            await layer.DeleteAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Null(await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestTransactionalDelete()
        {
            var model = new TestModel
            {
                forTest = "TestCreate",
                string1 = "test",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            await layer.DeleteAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), transaction, null, CancellationToken.None).ConfigureAwait(true);

            Assert.NotNull(await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Null(await layer.LoadAsync<TestModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestLoadNullThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAsync<TestModel>(string.Empty, (Key)null!, null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true);
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAsync<TestModel>(string.Empty, (IAsyncEnumerable<Key>)null!, null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestLoadMultiple()
        {
            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            Assert.All(returnedModels, x =>
            {
                Assert.NotNull(x.Key);
            });
            Assert.Equal(models, returnedModels);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModels = await layer.LoadAsync<TestModel>(string.Empty, returnedModels.Select(x => x.Key).ToAsyncEnumerable(), null, null, CancellationToken.None).Select(x => x.Value).ToArrayAsync().ConfigureAwait(true);

                Assert.Equal(models.Select(x => x.Key), loadedModels.Select(x => x!.Key));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestLoadMultipleMissing()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var keyFactory = await layer.GetKeyFactoryAsync<TestModel>(string.Empty, null, CancellationToken.None).ConfigureAwait(true);

            var models = new[]
            {
                new TestModel
                {
                    Key = keyFactory.CreateKey("TestLoadMultipleMissing-1"),
                    forTest = "TestLoadMultipleMissing",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestModel
                {
                    Key = keyFactory.CreateKey("TestLoadMultipleMissing-3"),
                    forTest = "TestLoadMultipleMissing",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var returnedModels = await layer.UpsertAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModels = await layer.LoadAsync<TestModel>(string.Empty, returnedModels.Select(x => x.Key).ToAsyncEnumerable(), null, null, CancellationToken.None).Select(x => x.Value).ToArrayAsync().ConfigureAwait(true);
                Assert.Equal(2, loadedModels.Length);
            }).ConfigureAwait(true);

            var nullLoadAttempt = await layer.LoadAsync<TestModel>(
                string.Empty,
                new[]
                {
                    models[0].Key,
                    keyFactory.CreateKey("TestLoadMultipleMissing-2"),
                    models[1].Key,
                    keyFactory.CreateKey("TestLoadMultipleMissing-4"),
                }.ToAsyncEnumerable(),
                null,
                null,
                CancellationToken.None).ToDictionaryAsync(k => k.Key, v => v.Value).ConfigureAwait(true);

            Assert.Equal(4, nullLoadAttempt.Count);
            Assert.Contains(keyFactory.CreateKey("TestLoadMultipleMissing-1"), nullLoadAttempt);
            Assert.Contains(keyFactory.CreateKey("TestLoadMultipleMissing-2"), nullLoadAttempt);
            Assert.Contains(keyFactory.CreateKey("TestLoadMultipleMissing-3"), nullLoadAttempt);
            Assert.Contains(keyFactory.CreateKey("TestLoadMultipleMissing-4"), nullLoadAttempt);
            Assert.NotNull(nullLoadAttempt[keyFactory.CreateKey("TestLoadMultipleMissing-1")]);
            Assert.Null(nullLoadAttempt[keyFactory.CreateKey("TestLoadMultipleMissing-2")]);
            Assert.NotNull(nullLoadAttempt[keyFactory.CreateKey("TestLoadMultipleMissing-3")]);
            Assert.Null(nullLoadAttempt[keyFactory.CreateKey("TestLoadMultipleMissing-4")]);
        }

        [Fact]
        public async Task TestLoadMultipleDuplicate()
        {
            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                }
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            Assert.All(returnedModels, x =>
            {
                Assert.NotNull(x.Key);
            });
            Assert.Equal(models, returnedModels);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(1, await layer.LoadAsync<TestModel>(string.Empty, new Key[] { models[0].Key, new Key(models[0].Key) }.ToAsyncEnumerable(), null, null, CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestTransactionalLoadMultiple()
        {
            var models = new[]
            {
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestModel
                {
                    forTest = "TestCreateMultiple",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            Assert.All(returnedModels, x =>
            {
                Assert.NotNull(x.Key);
            });
            Assert.Equal(models, returnedModels);

            var transaction = await layer.BeginTransactionAsync(string.Empty, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModels = await layer.LoadAsync<TestModel>(string.Empty, returnedModels.Select(x => x.Key).ToAsyncEnumerable(), transaction, null, CancellationToken.None).Select(x => x.Value).ToArrayAsync().ConfigureAwait(true);

                Assert.Equal(models.Select(x => x.Key), loadedModels.Select(x => x!.Key));
            }).ConfigureAwait(true);

            await layer.CommitAsync(string.Empty, transaction, null, CancellationToken.None).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestLoadAcrossNamespaces()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var models = new[]
            {
                new TestModel
                {
                    Key = (await layer.GetKeyFactoryAsync<TestModel>("a", null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                    forTest = "TestLoadAcrossNamespaces",
                    string1 = "test1",
                    number1 = 10,
                    number2 = 20,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
                new TestModel
                {
                    Key = (await layer.GetKeyFactoryAsync<TestModel>("b", null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                    forTest = "TestLoadAcrossNamespaces",
                    string1 = "test2",
                    number1 = 11,
                    number2 = 22,
                    timestamp = SystemClock.Instance.GetCurrentInstant(),
                },
            };

            await layer.CreateAsync("a", new[] { models[0] }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            await layer.CreateAsync("b", new[] { models[1] }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModels = await layer.LoadAcrossNamespacesAsync<TestModel>(models.Select(x => x.Key).ToAsyncEnumerable(), null, CancellationToken.None).Select(x => x.Value).ToArrayAsync().ConfigureAwait(true);

                Assert.Equal(models.Select(x => x.Key), loadedModels.Select(x => x!.Key));
            }).ConfigureAwait(true);

            Assert.Equal("keys", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAcrossNamespacesAsync<TestModel>(null!, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("keys", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAcrossNamespacesAsync<TestModel>(new Key[] { null! }.ToAsyncEnumerable(), null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
        }

        [Theory]
        [InlineData("", "a")]
        [InlineData("a", "b")]
        [InlineData("a", "")]
        public async Task TestMismatchedNamespaceIsCaughtOnCreate(string nsInModel, string nsOnOp)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var model = new TestModel
            {
                Key = (await layer.GetKeyFactoryAsync<TestModel>(nsInModel, null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                forTest = "TestMismatchedNamespaceIsCaughtOnCreate",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.CreateAsync(nsOnOp, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData("", "a")]
        [InlineData("a", "b")]
        [InlineData("a", "")]
        public async Task TestMismatchedNamespaceIsCaughtOnUpdate(string nsInModel, string nsOnOp)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var model = new TestModel
            {
                Key = (await layer.GetKeyFactoryAsync<TestModel>(nsInModel, null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                forTest = "TestMismatchedNamespaceIsCaughtOnCreate",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.UpdateAsync(nsOnOp, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData("", "a")]
        [InlineData("a", "b")]
        [InlineData("a", "")]
        public async Task TestMismatchedNamespaceIsCaughtOnUpsert(string nsInModel, string nsOnOp)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var model = new TestModel
            {
                Key = (await layer.GetKeyFactoryAsync<TestModel>(nsInModel, null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                forTest = "TestMismatchedNamespaceIsCaughtOnCreate",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.UpsertAsync(nsOnOp, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Theory]
        [InlineData("", "a")]
        [InlineData("a", "b")]
        [InlineData("a", "")]
        public async Task TestMismatchedNamespaceIsCaughtOnDelete(string nsInModel, string nsOnOp)
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var model = new TestModel
            {
                Key = (await layer.GetKeyFactoryAsync<TestModel>(nsInModel, null, CancellationToken.None).ConfigureAwait(true)).CreateIncompleteKey(),
                forTest = "TestMismatchedNamespaceIsCaughtOnCreate",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = SystemClock.Instance.GetCurrentInstant(),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await layer.DeleteAsync(nsOnOp, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestNullNamespaceThrowsException()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.QueryAsync<TestModel>(null!, x => true, null, null, null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAsync<TestModel>(null!, (Key)null!, null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.LoadAsync<TestModel>(null!, (IAsyncEnumerable<Key>)null!, null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.CreateAsync(null!, Array.Empty<TestModel>().ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.UpsertAsync(null!, Array.Empty<TestModel>().ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.UpdateAsync(null!, Array.Empty<TestModel>().ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.DeleteAsync(null!, Array.Empty<TestModel>().ToAsyncEnumerable(), null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.AllocateKeyAsync<TestModel>(null!, null, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.GetKeyFactoryAsync<TestModel>(null!, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.BeginTransactionAsync(null!, Repository.Transaction.TransactionMode.ReadWrite, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.RollbackAsync(null!, null!, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
            Assert.Equal("namespace", (await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await layer.CommitAsync(null!, null!, null, CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true)).ParamName);
        }

        [Fact]
        public void TestDefaultedModelHasCorrectDefaultsWhenConstructed()
        {
            var model = new DefaultedModel();

            Assert.Equal("test", model.myString);
            Assert.True(model.myBool);
            Assert.Equal(10, model.myInteger);
        }

        [Fact]
        public void TestDefaultedModelWithoutDefaultsOnValueTypesThrows()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                new DefaultedInvalidModel();
            });
        }

        [Fact]
        public async Task TestDefaultedModelHasDefaultsWhenLoaded()
        {
            // To test this, we first create with DefaultedBypassModel, and then load with DefaultedModel
            // to make sure it's defaulting on load.

            var model = new DefaultedBypassModel();

            Assert.Null(model.myString);
            Assert.Null(model.myBool);
            Assert.Null(model.myInteger);

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModel = await layer.LoadAsync<DefaultedModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal("test", loadedModel!.myString);
                Assert.True(loadedModel.myBool);
                Assert.Equal(10, loadedModel.myInteger);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestDefaultedModelHasDefaultsWhenSaved()
        {
            // To test this, we first create with DefaultedBypassModel, and then load with DefaultedModel
            // to make sure it's defaulting on load.

            var model = new DefaultedBypassModel();

            Assert.Null(model.myString);
            Assert.Null(model.myBool);
            Assert.Null(model.myInteger);

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var loadedModel = await layer.LoadAsync<DefaultedModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                Assert.Equal("test", loadedModel!.myString);
                Assert.True(loadedModel.myBool);
                Assert.Equal(10, loadedModel.myInteger);

                await layer.UpdateAsync(string.Empty, new[] { loadedModel }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

                await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
                {
                    model = await layer.LoadAsync<DefaultedBypassModel>(string.Empty, model.Key, null, null, CancellationToken.None).ConfigureAwait(true);

                    Assert.Equal("test", model!.myString);
                    Assert.True(model.myBool);
                    Assert.Equal(10, model.myInteger);
                }).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestPaginatedQueryWith4Entities()
        {
            const string name = "TestPaginatedQueryWith4Entities";
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 11,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 12,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 13,
                    number2 = 20,
                    timestamp = instant,
                }
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            // Just wait until we can load everything.
            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(4, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var set = await layer.QueryPaginatedAsync<TestModel>(
                string.Empty,
                null!,
                2,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(2, set.Results.Count);
            Assert.NotNull(set.NextCursor);

            var nextSet = await layer.QueryPaginatedAsync<TestModel>(
                string.Empty,
                set.NextCursor!,
                2,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(2, nextSet.Results.Count);

            // If we do have another page indicated, ensure that it returns an empty set of results.
            if (nextSet.NextCursor != null)
            {
                var finalSet = await layer.QueryPaginatedAsync<TestModel>(
                    string.Empty,
                    nextSet.NextCursor!,
                    2,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    CancellationToken.None).ConfigureAwait(true);
                Assert.Empty(finalSet.Results);
                Assert.Null(finalSet.NextCursor);
            }
        }

        [Fact]
        public async Task TestPaginatedQueryWith5Entities()
        {
            const string name = "TestPaginatedQueryWith5Entities";
            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 10,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 11,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 12,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 13,
                    number2 = 20,
                    timestamp = instant,
                },
                new TestModel
                {
                    forTest = name,
                    string1 = "test",
                    number1 = 14,
                    number2 = 20,
                    timestamp = instant,
                }
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            // Just wait until we can load everything.
            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(5, await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    null,
                    CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var set = await layer.QueryPaginatedAsync<TestModel>(
                string.Empty,
                null!,
                2,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(2, set.Results.Count);
            Assert.NotNull(set.NextCursor);

            var nextSet = await layer.QueryPaginatedAsync<TestModel>(
                string.Empty,
                set.NextCursor!,
                2,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(2, nextSet.Results.Count);
            Assert.NotNull(nextSet.NextCursor);

            var nextNextSet = await layer.QueryPaginatedAsync<TestModel>(
                string.Empty,
                nextSet.NextCursor!,
                2,
                x => x.timestamp == instant && x.forTest == name,
                null,
                null,
                null,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Single(nextNextSet.Results);

            if (nextNextSet.NextCursor == null)
            {
                // This is permitted and matches production (older Datastore emulators
                // can return a non-null cursor here).
            }
            else
            {
                var finalSet = await layer.QueryPaginatedAsync<TestModel>(
                    string.Empty,
                    nextNextSet.NextCursor,
                    2,
                    x => x.timestamp == instant && x.forTest == name,
                    null,
                    null,
                    null,
                    CancellationToken.None).ConfigureAwait(true);
                Assert.Empty(finalSet.Results);
                Assert.Null(finalSet.NextCursor);
            }
        }

        [Fact]
        public async Task TestGeographicQueriesSparse()
        {
            const string name = "TestGeographicQueriesSparse";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "melbourne",
                    location = new LatLng { Latitude = -37.8136, Longitude = 144.9631 },
                },
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "geelong",
                    location = new LatLng { Latitude = -38.1499, Longitude = 144.3617 },
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(
                    2,
                    await layer.QueryAsync<GeoSparseModel>(
                        string.Empty,
                        x => x.forTest == name && x.timestamp == instant,
                        null,
                        null,
                        null,
                        null,
                        CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            const float targetDistance = 35.0f;

            // Within distance of both locations (just barely).
            Assert.Equal(2, await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.91298645369961, Longitude = 144.61227791800124 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).CountAsync().ConfigureAwait(true));

            // Outside radius of both locations (just barely).
            Assert.Equal(0, await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88113253803819, Longitude = 144.5707632417446 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).CountAsync().ConfigureAwait(true));

            // Within radius of Geelong.
            var geelong = await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88412126495574, Longitude = 144.56687590591972 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);
            Assert.Single(geelong);
            Assert.Equal("geelong", geelong[0].descriptor);

            // Within radius of Melbourne.
            var melbourne = await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88131344781548, Longitude = 144.57575923614561 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);
            Assert.Single(melbourne);
            Assert.Equal("melbourne", melbourne[0].descriptor);
        }

        [Fact]
        public async Task TestGeographicQueriesDense()
        {
            const string name = "TestGeographicQueriesDense";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new GeoDenseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "melbourne",
                    location = new LatLng { Latitude = -37.8136, Longitude = 144.9631 },
                },
                new GeoDenseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "geelong",
                    location = new LatLng { Latitude = -38.1499, Longitude = 144.3617 },
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(
                    2,
                    await layer.QueryAsync<GeoDenseModel>(
                        string.Empty,
                        x => x.forTest == name && x.timestamp == instant,
                        null,
                        null,
                        null,
                        null,
                        CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            const float targetDistance = 35.0f;

            // Within distance of both locations (just barely).
            Assert.Equal(2, await layer.QueryAsync<GeoDenseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.91298645369961, Longitude = 144.61227791800124 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).CountAsync().ConfigureAwait(true));

            // Outside radius of both locations (just barely).
            Assert.Equal(0, await layer.QueryAsync<GeoDenseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88113253803819, Longitude = 144.5707632417446 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).CountAsync().ConfigureAwait(true));

            // Within radius of Geelong.
            var geelong = await layer.QueryAsync<GeoDenseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88412126495574, Longitude = 144.56687590591972 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);
            Assert.Single(geelong);
            Assert.Equal("geelong", geelong[0].descriptor);

            // Within radius of Melbourne.
            var melbourne = await layer.QueryAsync<GeoDenseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -37.88131344781548, Longitude = 144.57575923614561 }, targetDistance),
                null,
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);
            Assert.Single(melbourne);
            Assert.Equal("melbourne", melbourne[0].descriptor);
        }

        [Fact]
        public async Task TestGeographicQueriesOrderNearest()
        {
            const string name = "TestGeographicQueriesOrderNearest";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "melbourne",
                    location = new LatLng { Latitude = -37.8136, Longitude = 144.9631 },
                },
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "geelong",
                    location = new LatLng { Latitude = -38.1499, Longitude = 144.3617 },
                },
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "ballarat",
                    location = new LatLng { Latitude = -37.5622, Longitude = 143.8503 },
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(
                    3,
                    await layer.QueryAsync<GeoSparseModel>(
                        string.Empty,
                        x => x.forTest == name && x.timestamp == instant,
                        null,
                        null,
                        null,
                        null,
                        CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var results = await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -38.250966756220556, Longitude = 144.58046654644946 }, 100.0f),
                x => x.location!.Nearest(),
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);

            Assert.Equal(3, results.Count);
            Assert.Equal("geelong", results[0].descriptor);
            Assert.Equal("melbourne", results[1].descriptor);
            Assert.Equal("ballarat", results[2].descriptor);
        }

        [Fact]
        public async Task TestGeographicQueriesOrderFurthest()
        {
            const string name = "TestGeographicQueriesOrderFurthest";

            var instant = SystemClock.Instance.GetCurrentInstant();

            var models = new[]
            {
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "melbourne",
                    location = new LatLng { Latitude = -37.8136, Longitude = 144.9631 },
                },
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "geelong",
                    location = new LatLng { Latitude = -38.1499, Longitude = 144.3617 },
                },
                new GeoSparseModel
                {
                    forTest = name,
                    timestamp = instant,
                    descriptor = "ballarat",
                    location = new LatLng { Latitude = -37.5622, Longitude = 143.8503 },
                },
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var returnedModels = await layer.CreateAsync(string.Empty, models.ToAsyncEnumerable(), null, null, CancellationToken.None).ToArrayAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.Equal(
                    3,
                    await layer.QueryAsync<GeoSparseModel>(
                        string.Empty,
                        x => x.forTest == name && x.timestamp == instant,
                        null,
                        null,
                        null,
                        null,
                        CancellationToken.None).CountAsync().ConfigureAwait(true));
            }).ConfigureAwait(true);

            var results = await layer.QueryAsync<GeoSparseModel>(
                string.Empty,
                x =>
                    x.forTest == name && x.timestamp == instant &&
                    x.location!.WithinKilometers(new LatLng { Latitude = -38.250966756220556, Longitude = 144.58046654644946 }, 100.0f),
                x => x.location!.Furthest(),
                null,
                null,
                null,
                CancellationToken.None).ToListAsync().ConfigureAwait(true);

            Assert.Equal(3, results.Count);
            Assert.Equal("ballarat", results[0].descriptor);
            Assert.Equal("melbourne", results[1].descriptor);
            Assert.Equal("geelong", results[2].descriptor);
        }

        [Fact]
        public async Task TestCreateAndLoadWithEmbeddedEntity()
        {
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

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
                forTest = "TestCreateAndLoadWithEmbeddedEntity",
                timestamp = SystemClock.Instance.GetCurrentInstant(),
                entity = entity,
            };

            var returnedModel = await layer.CreateAsync(string.Empty, new[] { model }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            Assert.NotNull(model.Key);
            Assert.NotNull(returnedModel.Key);
            Assert.Equal(model.Key, returnedModel.Key);
            Assert.Equal(model, returnedModel);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                Assert.NotNull(await layer.LoadAsync<EmbeddedEntityModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true));
            }).ConfigureAwait(true);

            var loadedModel = await layer.LoadAsync<EmbeddedEntityModel>(string.Empty, returnedModel.Key, null, null, CancellationToken.None).ConfigureAwait(true);

            Assert.NotEqual(loadedModel, model);
            Assert.NotNull(loadedModel!.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                DatastoreRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
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
            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var subentity3 = new Entity();
            subentity3.Key = null;
            subentity3["a"] = "blah";

            var entity = new Entity();
            entity.Key = null;
            entity["null"] = Value.ForNull();
            entity["string"] = "test";
            entity["integer"] = 5;
            entity["double"] = 5.0;
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

            const string name = "TestCreateAndQueryWithEmbeddedEntity";
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

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
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
            Assert.NotNull(loadedModel.timestamp);
            Assert.NotNull(loadedModel.entity);
            foreach (var property in entity.Properties)
            {
                DatastoreRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
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
                DatastoreRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }

            loadedModel = await layer.QueryAsync<EmbeddedEntityModel>(
                string.Empty,
                x =>
                    x.forTest == name &&
                    x.timestamp == timestamp &&
                    x.entity!["entity"].EntityValue["a"].StringValue == "blah",
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
                DatastoreRepositoryLayerTests.AssertProperty(loadedModel.entity!, property.Key, property.Value);
            }
        }

        [Fact]
        public async Task TestQueryAnyString()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model1 = new TestModel
            {
                forTest = "TestQueryAnyString",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
                stringArray = ["abc", "hello", "world"]
            };
            var model2 = new TestModel
            {
                forTest = "TestQueryAnyString",
                string1 = "test2",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
                stringArray = ["abc2", "hello2", "world2"]
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model1, model2 }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestQueryAnyString" && x.stringArray.IsAnyString("hello2"),
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model2.Key);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryOneOfString()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model1 = new TestModel
            {
                forTest = "TestQueryOneOfString",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };
            var model2 = new TestModel
            {
                forTest = "TestQueryOneOfString",
                string1 = "test2",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };
            var model3 = new TestModel
            {
                forTest = "TestQueryOneOfString",
                string1 = "test3",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model1, model2, model3 }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestQueryOneOfString" && x.string1.IsOneOfString(new[] { "abc", "hello", "test2" }),
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model2.Key);
            }).ConfigureAwait(true);
        }

        [Fact]
        public async Task TestQueryNotOneOfString()
        {
            var instant = SystemClock.Instance.GetCurrentInstant();

            var model1 = new TestModel
            {
                forTest = "TestQueryNotOneOfString",
                string1 = "test1",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };
            var model2 = new TestModel
            {
                forTest = "TestQueryNotOneOfString",
                string1 = "test2",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };
            var model3 = new TestModel
            {
                forTest = "TestQueryNotOneOfString",
                string1 = "test3",
                number1 = 10,
                number2 = 20,
                timestamp = instant,
            };

            var layer = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            await layer.CreateAsync(string.Empty, new[] { model1, model2, model3 }.ToAsyncEnumerable(), null, null, CancellationToken.None).FirstAsync().ConfigureAwait(true);

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                var result = await layer.QueryAsync<TestModel>(
                    string.Empty,
                    x => x.timestamp == instant && x.forTest == "TestQueryNotOneOfString" && x.string1.IsNotOneOfString(new[] { "abc", "hello", "blah", "test2", "test3" }),
                    null,
                    1,
                    null,
                    null,
                    CancellationToken.None).FirstOrDefaultAsync().ConfigureAwait(true);

                Assert.NotNull(result);
                Assert.Equal(result.Key, model1.Key);
            }).ConfigureAwait(true);
        }
    }
}
