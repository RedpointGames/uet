namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    using Xunit;

    public class WorkerCoreRequestCollectionTests
    {
        [Fact]
        public async Task NotifiesThroughStateChanges()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var notificationCount = 0;

            var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
            await collection.OnRequestsChanged.AddAsync((args, ct) =>
            {
                notificationCount++;
                return Task.CompletedTask;
            });

            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }

            await using (var localRequest = await collection.CreateUnfulfilledRequestAsync(true, cancellationToken))
            {
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(1, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                await using (var remoteRequest = await collection.CreateUnfulfilledRequestAsync(false, cancellationToken))
                {
                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(1, stats.UnfulfilledLocalRequests);
                        Assert.Equal(1, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(0, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }

                    await ((WorkerCoreRequestCollection<CollectionTestingWorkerCore>.WorkerCoreRequest)localRequest).FulfillRequestWithinLockAsync(new CollectionTestingWorkerCore());

                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(0, stats.UnfulfilledLocalRequests);
                        Assert.Equal(1, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(1, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }

                    await ((WorkerCoreRequestCollection<CollectionTestingWorkerCore>.WorkerCoreRequest)remoteRequest).FulfillRequestWithinLockAsync(new CollectionTestingWorkerCore());

                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(0, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(1, stats.FulfilledLocalRequests);
                        Assert.Equal(1, stats.FulfilledRemotableRequests);
                    }
                }

                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(1, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }
            }

            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }

            Assert.Equal(6, notificationCount);
        }

        [Fact]
        public async Task CreateFulfilledRequestCleansUpOnTimeout()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await collection.CreateFulfilledRequestAsync(true, new CancellationTokenSource(500).Token);
            });

            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }
        }

        [Fact]
        public async Task CreateFulfilledRequestCleansUpOnImmediateCancel()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var collection = new WorkerCoreRequestCollection<CollectionTestingWorkerCore>();
            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }

            var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await collection.CreateFulfilledRequestAsync(true, cts.Token);
            });

            {
                var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                Assert.Equal(0, stats.UnfulfilledLocalRequests);
                Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                Assert.Equal(0, stats.FulfilledLocalRequests);
                Assert.Equal(0, stats.FulfilledRemotableRequests);
            }
        }
    }
}