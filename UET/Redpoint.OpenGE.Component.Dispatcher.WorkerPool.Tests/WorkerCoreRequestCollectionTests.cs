namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System.Threading;
    using Xunit;

    public class WorkerCoreRequestCollectionTests
    {
        [Fact]
        public async Task NotifiesThroughStateChanges()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var notificationCount = 0;

                var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
                await collection.OnRequestsChanged.AddAsync((args, ct) =>
                {
                    notificationCount++;
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                await using ((await collection.CreateUnfulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var localRequest).ConfigureAwait(false))
                {
                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                        Assert.Equal(1, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(0, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }

                    await using ((await collection.CreateUnfulfilledRequestAsync(CoreAllocationPreference.PreferRemote, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var remoteRequest).ConfigureAwait(false))
                    {
                        {
                            var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                            Assert.Equal(1, stats.UnfulfilledLocalRequests);
                            Assert.Equal(1, stats.UnfulfilledRemotableRequests);
                            Assert.Equal(0, stats.FulfilledLocalRequests);
                            Assert.Equal(0, stats.FulfilledRemotableRequests);
                        }

                        await ((WorkerCoreRequest<CollectionTestingWorkerCore>)localRequest).FulfillRequestWithinLockAsync(new CollectionTestingWorkerCore()).ConfigureAwait(false);

                        {
                            var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                            Assert.Equal(0, stats.UnfulfilledLocalRequests);
                            Assert.Equal(1, stats.UnfulfilledRemotableRequests);
                            Assert.Equal(1, stats.FulfilledLocalRequests);
                            Assert.Equal(0, stats.FulfilledRemotableRequests);
                        }

                        await ((WorkerCoreRequest<CollectionTestingWorkerCore>)remoteRequest).FulfillRequestWithinLockAsync(new CollectionTestingWorkerCore()).ConfigureAwait(false);

                        {
                            var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                            Assert.Equal(0, stats.UnfulfilledLocalRequests);
                            Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                            Assert.Equal(1, stats.FulfilledLocalRequests);
                            Assert.Equal(1, stats.FulfilledRemotableRequests);
                        }
                    }

                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                        Assert.Equal(0, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(1, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }
                }

                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                Assert.Equal(6, notificationCount);
            }
        }

        [Fact]
        public async Task CreateFulfilledRequestCleansUpOnTimeout()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await collection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, new CancellationTokenSource(500).Token).ConfigureAwait(false);
            }).ConfigureAwait(false);

            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }
        }

        [Fact]
        public async Task CreateFulfilledRequestCleansUpOnImmediateCancel()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                var cts = new CancellationTokenSource();
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    await collection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cts.Token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken).ConfigureAwait(false);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }
            }
        }
    }
}