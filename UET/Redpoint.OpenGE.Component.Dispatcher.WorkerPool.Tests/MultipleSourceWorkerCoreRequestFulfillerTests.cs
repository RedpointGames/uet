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
    using Redpoint.Concurrency;

    public class MultipleSourceWorkerCoreRequestFulfillerTests
    {
        private static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTasks();
            return services.BuildServiceProvider();
        }

        private static int GetBigIterationCount()
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
            {
                return 1;
            }
            else
            {
                return 1000;
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillSingleRequest()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider).ConfigureAwait(false);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    await using (var request = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsOneWorker()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider).ConfigureAwait(false);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    await using (var request1 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                        await using (var request2 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                        {
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsOneWorker()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider).ConfigureAwait(false);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    await using (var request1 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                    }

                    await using (var request2 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkers()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
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
                    await providerCollection.AddAsync(provider1).ConfigureAwait(false);
                    var provider2 = new DynamicCoreProvider(1);
                    await providerCollection.AddAsync(provider2).ConfigureAwait(false);

                    await using (new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                        logger,
                        sp.GetRequiredService<ITaskScheduler>(),
                        requestCollection,
                        providerCollection,
                        true).AsAsyncDisposable(out var fulfiller).ConfigureAwait(false))
                    {
                        fulfiller.SetTracer(tracer);
                        await using (var request1 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                        {
                            await using (var request2 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                }
                catch
                {
                    var messages = tracer.DumpAllMessages();
                    Assert.Fail(string.Join("\n", messages));
                    throw;
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkersOvercapacity()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider1 = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider1).ConfigureAwait(false);
                var provider2 = new DynamicCoreProvider(2);
                await providerCollection.AddAsync(provider2).ConfigureAwait(false);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    await using (var request1 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                        await using (var request2 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                        {
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsTwoWorkers()
        {
            for (int i = 0; i < GetBigIterationCount(); i++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                var provider1 = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider1).ConfigureAwait(false);
                var provider2 = new DynamicCoreProvider(1);
                await providerCollection.AddAsync(provider2).ConfigureAwait(false);

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    await using (var request1 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                    }

                    await using (var request2 = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                    {
                    }
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallel()
        {
            for (int z = 0; z < GetBigIterationCount(); z++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                for (int i = 0; i < 10; i++)
                {
                    var provider = new DynamicCoreProvider(20);
                    await providerCollection.AddAsync(provider).ConfigureAwait(false);
                }

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    long coresFulfilled = 0;
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, 200).ToAsyncEnumerable(),
                        async (index, _) =>
                        {
                            await using (var request = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                            {
                                Interlocked.Increment(ref coresFulfilled);
                            }
                        }).ConfigureAwait(false);
                    Assert.Equal(200, coresFulfilled);
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallelWithDyingCores()
        {
            for (int z = 0; z < GetBigIterationCount(); z++)
            {
                var cancellationToken = new CancellationTokenSource(5000).Token;

                var sp = BuildServiceProvider();
                var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

                var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
                var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
                for (int i = 0; i < 10; i++)
                {
                    var provider = new DyingDynamicCoreProvider(20);
                    await providerCollection.AddAsync(provider).ConfigureAwait(false);
                }

                await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                    logger,
                    sp.GetRequiredService<ITaskScheduler>(),
                    requestCollection,
                    providerCollection,
                    true).ConfigureAwait(false))
                {
                    long coresFulfilled = 0;
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, 200).ToAsyncEnumerable(),
                        async (index, _) =>
                        {
                            await using (var request = (await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
                            {
                                Interlocked.Increment(ref coresFulfilled);
                            }
                        }).ConfigureAwait(false);
                    Assert.Equal(200, coresFulfilled);
                }
            }
        }
    }
}
