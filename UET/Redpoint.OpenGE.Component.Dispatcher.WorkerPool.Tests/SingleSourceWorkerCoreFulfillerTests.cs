namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class SingleSourceWorkerCoreFulfillerTests
    {
        private IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTasks();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task SingleSourceCanFulfillLocalRequestsViaNotification()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var testProvider = new ManualCoreProvider();

                var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    collection,
                    testProvider,
                    true,
                    0))
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
        }

        [Fact]
        public async Task SingleSourceCanFulfillLocalRequestsViaWaitForCoreAsync()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var testProvider = new ManualCoreProvider();

                var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    collection,
                    testProvider,
                    true,
                    0))
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
        }

        [Fact]
        public async Task SourceSourceCanCreateFulfilledRequests()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var testProvider = new ManualCoreProvider();

                var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    collection,
                    testProvider,
                    true,
                    0))
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
        }

        [Fact]
        public async Task SingleSourceCanFulfillRequestsInParallel()
        {
            for (int i = 0; i < 1000; i++)
            {
                var tracer = new WorkerPoolTracer();
                try
                {
                    var cancellationToken = new CancellationTokenSource(5000).Token;

                    var sp = BuildServiceProvider();
                    var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                    var testProvider = new ManualCoreProvider();
                    testProvider.ReleaseCores(24);

                    var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                    collection.SetTracer(tracer);
                    await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                        logger,
                        sp.GetRequiredService<ITaskScheduler>(),
                        collection,
                        testProvider,
                        true,
                        0))
                    {
                        fulfiller.SetTracer(tracer);
                        long coresFulfilled = 0;
                        await Parallel.ForEachAsync(
                            Enumerable.Range(0, 24).ToAsyncEnumerable(),
                            async (index, _) =>
                            {
                                await using (var request = await collection.CreateFulfilledRequestAsync(
                                    Random.Shared.Next(0, 3) switch
                                    {
                                        0 => CoreAllocationPreference.RequireLocal,
                                        1 => CoreAllocationPreference.PreferLocal,
                                        _ => CoreAllocationPreference.PreferRemote,
                                    },
                                    cancellationToken))
                                {
                                    Interlocked.Increment(ref coresFulfilled);
                                }
                            });
                        Assert.Equal(24, coresFulfilled);
                    }
                }
                catch
                {
                    var messages = tracer.DumpAllMessages();
                    Assert.True(false, string.Join("\n", messages));
                    throw;
                }
            }
        }

        [Fact]
        public async Task SingleSourceCanFulfillRequestsInParallelWithRemoteDelayEnabled()
        {
            for (int i = 0; i < 10; i++)
            {
                var tracer = new WorkerPoolTracer();
                try
                {
                    var cancellationToken = new CancellationTokenSource(5000).Token;

                    var sp = BuildServiceProvider();
                    var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                    var testProvider = new ManualCoreProvider();
                    testProvider.ReleaseCores(24);

                    var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                    collection.SetTracer(tracer);
                    await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                        logger,
                        sp.GetRequiredService<ITaskScheduler>(),
                        collection,
                        testProvider,
                        true,
                        100))
                    {
                        fulfiller.SetTracer(tracer);
                        long coresFulfilled = 0;
                        await Parallel.ForEachAsync(
                            Enumerable.Range(0, 24).ToAsyncEnumerable(),
                            async (index, _) =>
                            {
                                await using (var request = await collection.CreateFulfilledRequestAsync(
                                    Random.Shared.Next(0, 3) switch
                                    {
                                        0 => CoreAllocationPreference.RequireLocal,
                                        1 => CoreAllocationPreference.PreferLocal,
                                        _ => CoreAllocationPreference.PreferRemote,
                                    },
                                    cancellationToken))
                                {
                                    Interlocked.Increment(ref coresFulfilled);
                                }
                            });
                        Assert.Equal(24, coresFulfilled);
                    }
                }
                catch
                {
                    var messages = tracer.DumpAllMessages();
                    Assert.True(false, string.Join("\n", messages));
                    throw;
                }
            }
        }

        [Fact]
        public async Task SingleSourceCanFulfillRequestsInParallelWithDyingCores()
        {
            for (int i = 0; i < 1000; i++)
            {
                var tracer = new WorkerPoolTracer();
                try
                {
                    var cancellationToken = new CancellationTokenSource(5000).Token;

                    var sp = BuildServiceProvider();
                    var logger = sp.GetRequiredService<ILogger<SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                    var testProvider = new DyingDynamicCoreProvider(24);

                    var collection = new WorkerCoreRequestCollection<IWorkerCore>();
                    collection.SetTracer(tracer);
                    await using (var fulfiller = new SingleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                        logger,
                        sp.GetRequiredService<ITaskScheduler>(),
                        collection,
                        testProvider,
                        true,
                        0))
                    {
                        fulfiller.SetTracer(tracer);
                        long coresFulfilled = 0;
                        await Parallel.ForEachAsync(
                            Enumerable.Range(0, 200).ToAsyncEnumerable(),
                            async (index, _) =>
                            {
                                await using (var request = await collection.CreateFulfilledRequestAsync(
                                    Random.Shared.Next(0, 3) switch
                                    {
                                        0 => CoreAllocationPreference.RequireLocal,
                                        1 => CoreAllocationPreference.PreferLocal,
                                        _ => CoreAllocationPreference.PreferRemote,
                                    },
                                    cancellationToken))
                                {
                                    Interlocked.Increment(ref coresFulfilled);
                                }
                            });
                        Assert.Equal(200, coresFulfilled);
                    }
                }
                catch
                {
                    var messages = tracer.DumpAllMessages();
                    Assert.True(false, string.Join("\n", messages));
                    throw;
                }
            }
        }
    }
}
