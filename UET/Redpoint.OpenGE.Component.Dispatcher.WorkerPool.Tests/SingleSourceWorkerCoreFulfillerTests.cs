namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class SingleSourceWorkerCoreFulfillerTests
    {
        [Fact]
        public async Task SingleSourceCanFulfillLocalRequestsViaNotification()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var testProvider = new ManualCoreProvider();

            var collection = new WorkerCoreRequestCollection<IWorkerCore>();
            await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
                collection,
                testProvider,
                true))
            {
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                await using (var localRequest = await collection.CreateUnfulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                {
                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(1, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(0, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }

                    var gate = new Gate();
                    await collection.OnRequestsChanged.AddAsync((_, _) =>
                    {
                        gate.Open();
                        return Task.CompletedTask;
                    });
                    testProvider.ReleaseCore();
                    await gate.WaitAsync();

                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(0, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(1, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }
                }
            }
        }

        [Fact]
        public async Task SingleSourceCanFulfillLocalRequestsViaWaitForCoreAsync()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var testProvider = new ManualCoreProvider();

            var collection = new WorkerCoreRequestCollection<IWorkerCore>();
            await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
                collection,
                testProvider,
                true))
            {
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                await using (var localRequest = await collection.CreateUnfulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                {
                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(1, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(0, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }

                    testProvider.ReleaseCore();
                    await localRequest.WaitForCoreAsync(cancellationToken);

                    {
                        var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                        Assert.Equal(0, stats.UnfulfilledLocalRequests);
                        Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                        Assert.Equal(1, stats.FulfilledLocalRequests);
                        Assert.Equal(0, stats.FulfilledRemotableRequests);
                    }
                }
            }
        }

        [Fact]
        public async Task SourceSourceCanCreateFulfilledRequests()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var testProvider = new ManualCoreProvider();

            var collection = new WorkerCoreRequestCollection<IWorkerCore>();
            await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
                collection,
                testProvider,
                true))
            {
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                var fulfilledRequest = Task.Run(async () =>
                {
                    return await collection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken);
                });
                testProvider.ReleaseCore();
                await using (var request = await fulfilledRequest)
                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(1, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }

                {
                    var stats = await collection.GetCurrentStatisticsAsync(cancellationToken);
                    Assert.Equal(0, stats.UnfulfilledLocalRequests);
                    Assert.Equal(0, stats.UnfulfilledRemotableRequests);
                    Assert.Equal(0, stats.FulfilledLocalRequests);
                    Assert.Equal(0, stats.FulfilledRemotableRequests);
                }
            }
        }

        [Fact]
        public async Task SingleSourceCanFulfillRequestsInParallel()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var testProvider = new ManualCoreProvider();
            testProvider.ReleaseCores(24);

            var collection = new WorkerCoreRequestCollection<IWorkerCore>();
            await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
                collection,
                testProvider,
                true))
            {
                long coresFulfilled = 0;
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, 24).ToAsyncEnumerable(),
                    async (index, _) =>
                    {
                        await using (var request = await collection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                            Interlocked.Increment(ref coresFulfilled);
                        }
                    });
                Assert.Equal(24, coresFulfilled);
            }
        }
    }
}
