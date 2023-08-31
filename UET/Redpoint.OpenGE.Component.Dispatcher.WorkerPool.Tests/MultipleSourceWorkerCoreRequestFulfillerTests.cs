namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class MultipleSourceWorkerCoreRequestFulfillerTests
    {
        [Fact]
        public async Task MultipleSourceCanFulfillSingleRequest()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider = new DynamicCoreProvider(1);
            await providerCollection.AddAsync(provider);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
                requestCollection,
                providerCollection,
                true))
            {
                await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                {
                }
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsOneWorker()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider = new DynamicCoreProvider(2);
            await providerCollection.AddAsync(provider);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
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

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsOneWorker()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider = new DynamicCoreProvider(2);
            await providerCollection.AddAsync(provider);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
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

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkers()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider1 = new DynamicCoreProvider(1);
            await providerCollection.AddAsync(provider1);
            var provider2 = new DynamicCoreProvider(1);
            await providerCollection.AddAsync(provider2);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
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

        [Fact]
        public async Task MultipleSourceCanFulfillTwoParallelRequestsTwoWorkersOvercapacity()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider1 = new DynamicCoreProvider(2);
            await providerCollection.AddAsync(provider1);
            var provider2 = new DynamicCoreProvider(2);
            await providerCollection.AddAsync(provider2);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
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

        [Fact]
        public async Task MultipleSourceCanFulfillTwoSequentialRequestsTwoWorkers()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<IWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<IWorkerCore>();
            var provider1 = new DynamicCoreProvider(1);
            await providerCollection.AddAsync(provider1);
            var provider2 = new DynamicCoreProvider(1);
            await providerCollection.AddAsync(provider2);

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<IWorkerCore>(
                logger,
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

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallel()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
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
                requestCollection,
                providerCollection,
                true))
            {
                var coresFulfilled = 0;
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, 200).ToAsyncEnumerable(),
                    async (index, _) =>
                    {
                        await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                            coresFulfilled++;
                        }
                    });
                Assert.Equal(200, coresFulfilled);
            }
        }

        [Fact]
        public async Task MultipleSourceCanFulfillLotsOfRequestsInParallelWithDyingCores()
        {
            var cancellationToken = new CancellationTokenSource(5000).Token;

            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
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
                requestCollection,
                providerCollection,
                true))
            {
                var coresFulfilled = 0;
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, 200).ToAsyncEnumerable(),
                    async (index, _) =>
                    {
                        await using (var request = await requestCollection.CreateFulfilledRequestAsync(CoreAllocationPreference.RequireLocal, cancellationToken))
                        {
                            coresFulfilled++;
                        }
                    });
                Assert.Equal(200, coresFulfilled);
            }
        }
    }
}
