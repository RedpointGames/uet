namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class MultipleSourceWorkerCoreRequestFulfillerTests
    {
        private IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTasks();
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task MultipleSourceCanFulfillSingleRequest()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsOneWorker()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    await using (var request1 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                        await using (var request2 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsOneWorker()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    await using (var request1 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                    }

                    await using (var request2 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkers()
        {
            for (int i = 0; i < 1000; i++)
            {
                var tracer = new ConcurrentWorkerPoolTracer();
                try
                {
                    var cancellationToken = new CancellationTokenSource(5000).Token;

                    var sp = BuildServiceProvider();
                    var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                    var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                    requestCollection.SetTracer(tracer);
                    var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                    providerCollection.SetTracer(tracer);
                    var provider1 = new DynamicCoreProvider(1);
                    await providerCollection.AddAsync(provider1);
                    var provider2 = new DynamicCoreProvider(1);
                    await providerCollection.AddAsync(provider2);

                    await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                        logger,
                        sp.GetRequiredService<ITaskScheduler>(),
                        requestCollection,
                        providerCollection,
                        true))
                    {
                        fulfiller.SetTracer(tracer);
                        await using (var request1 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                            await using (var request2 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                            {
                            }
                        }
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
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkersOvercapacity()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider1 = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider1);
                var provider2 = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider2);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    await using (var request1 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                        await using (var request2 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsTwoWorkers()
        {
            for (int i = 0; i < 1000; i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider1 = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider1);
                var provider2 = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider2);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    await using (var request1 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                    }

                    await using (var request2 = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallel()
        {
            for (int z = 0; z < 1000; z++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                for (int i = 0; i < 10; i++)
                {
                    var provider = new DynamicCoreProvider(20);
                    await providerCollection.AddAsync(provider);
                }

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    long coresFulfilled = 0;
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, 200).ToAsyncEnumerable(),
                        async (index, _) =>
                        {
                            await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                            {
                                Interlocked.Increment(ref coresFulfilled);
                            }
                        });
                    Assert.Equal(200, coresFulfilled);
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallelWithDyingCores()
        {
            for (int z = 0; z < 1000; z++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                for (int i = 0; i < 10; i++)
                {
                    var provider = new DyingDynamicCoreProvider(20);
                    await providerCollection.AddAsync(provider);
                }

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true))
                {
                    long coresFulfilled = 0;
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, 200).ToAsyncEnumerable(),
                        async (index, _) =>
                        {
                            await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                            {
                                Interlocked.Increment(ref coresFulfilled);
                            }
                        });
                    Assert.Equal(200, coresFulfilled);
                }
            }
        }
    }
}
