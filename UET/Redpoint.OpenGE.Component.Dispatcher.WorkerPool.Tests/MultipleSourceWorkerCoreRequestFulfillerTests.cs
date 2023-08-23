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
        public async Task MultipleSourceCanFulfillRequestsInParallel()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILogger<MultipleSourceWorkerCoreRequestFulfiller<NullWorkerCore>>>();

            var requestCollection = new WorkerCoreRequestCollection<NullWorkerCore>();
            var providerCollection = new WorkerCoreProviderCollection<NullWorkerCore>();
            for (int i = 0; i < 10; i++)
            {
                var provider = new TestCoreProvider();
                provider.ProvideCore.Release(20);
                await providerCollection.AddAsync(provider);
            }

            await using (var fulfiller = new MultipleSourceWorkerCoreRequestFulfiller<NullWorkerCore>(
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
                        await using (var request = await requestCollection.CreateFulfilledRequestAsync(true, CancellationToken.None))
                        {
                            coresFulfilled++;
                        }
                    });
                Assert.Equal(200, coresFulfilled);
            }
        }
    }
}
