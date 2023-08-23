namespace Redpoint.OpenGE.Component.Dispatcher.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using Xunit;

    public class WorkerPoolTests
    {
        [Fact]
        public async Task MultipleWorkersReservedForSingleRequest()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGrpcPipes();
            services.AddAutoDiscovery();
            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IGrpcPipeFactory>();

            var worker1Guid = Guid.NewGuid().ToString();
            var worker2Guid = Guid.NewGuid().ToString();

            var server1 = new ReservationTestingTaskServer(1000, 1);
            var server2 = new ReservationTestingTaskServer(500, 1);

            var worker1 = factory.CreateServer(
                worker1Guid,
                GrpcPipeNamespace.User,
                server1);
            var worker2 = factory.CreateServer(
                worker2Guid,
                GrpcPipeNamespace.User,
                server2);
            await worker1.StartAsync();
            await worker2.StartAsync();

            var worker1Client = factory.CreateClient(
                worker1Guid,
                GrpcPipeNamespace.User,
                channel => new TaskApi.TaskApiClient(channel));
            var worker2Client = factory.CreateClient(
                worker2Guid,
                GrpcPipeNamespace.User,
                channel => new TaskApi.TaskApiClient(channel));

            await using var pool = new DefaultWorkerPool(
                provider.GetRequiredService<ILogger<DefaultWorkerPool>>(),
                provider.GetRequiredService<ILogger<WorkerSubpool>>(),
                provider.GetRequiredService<INetworkAutoDiscovery>(),
                null);

            await pool._remoteSubpool.RegisterWorkerAsync(
                new WorkerAddRequest
                {
                    DisplayName = "Test Client 1",
                    UniqueId = "1",
                    Client = worker1Client,
                });
            await pool._remoteSubpool.RegisterWorkerAsync(
                new WorkerAddRequest
                {
                    DisplayName = "Test Client 2",
                    UniqueId = "2",
                    Client = worker2Client,
                });

            await using var reservation = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);

            Assert.True(server1.Reserved == 1, "Server 1 should be reserved.");
            Assert.True(server2.Reserved == 1, "Server 2 should be reserved.");

            await Task.Delay(750);

            Assert.True(
                (server1.Reserved == 1 || server2.Reserved == 1) &&
                !(server1.Reserved == 1 && server2.Reserved == 1), "Only one server should be reserved.");

            await Task.Delay(500);

            Assert.False(server1.Reserved == 1, "Server 1 should not be reserved.");
            Assert.False(server2.Reserved == 1, "Server 2 should not be reserved.");
        }

        [Fact]
        public async Task WorkerHasExpectedCoreCountReservedForSingleRequest()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGrpcPipes();
            services.AddAutoDiscovery();
            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IGrpcPipeFactory>();

            var worker1Guid = Guid.NewGuid().ToString();
            var worker2Guid = Guid.NewGuid().ToString();

            var server1 = new ReservationTestingTaskServer(500, 8);

            var worker1 = factory.CreateServer(
                worker1Guid,
                GrpcPipeNamespace.User,
                server1);
            await worker1.StartAsync();

            var worker1Client = factory.CreateClient(
                worker1Guid,
                GrpcPipeNamespace.User,
                channel => new TaskApi.TaskApiClient(channel));

            await using var pool = new DefaultWorkerPool(
                provider.GetRequiredService<ILogger<DefaultWorkerPool>>(),
                provider.GetRequiredService<ILogger<WorkerSubpool>>(),
                provider.GetRequiredService<INetworkAutoDiscovery>(),
                null);

            await pool._remoteSubpool.RegisterWorkerAsync(
                new WorkerAddRequest
                {
                    DisplayName = "Test Client 1",
                    UniqueId = "1",
                    Client = worker1Client,
                });

            await using var reservation = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);

            Assert.Equal(1, server1.Reserved);

            await Task.Delay(1000);

            Assert.Equal(0, server1.Reserved);
        }

        [Fact]
        public async Task WorkerHasExpectedCoreCountReservedForFourRequests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGrpcPipes();
            services.AddAutoDiscovery();
            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IGrpcPipeFactory>();

            var worker1Guid = Guid.NewGuid().ToString();
            var worker2Guid = Guid.NewGuid().ToString();

            var server1 = new ReservationTestingTaskServer(500, 8);

            var worker1 = factory.CreateServer(
                worker1Guid,
                GrpcPipeNamespace.User,
                server1);
            await worker1.StartAsync();

            var worker1Client = factory.CreateClient(
                worker1Guid,
                GrpcPipeNamespace.User,
                channel => new TaskApi.TaskApiClient(channel));

            await using var pool = new DefaultWorkerPool(
                provider.GetRequiredService<ILogger<DefaultWorkerPool>>(),
                provider.GetRequiredService<ILogger<WorkerSubpool>>(),
                provider.GetRequiredService<INetworkAutoDiscovery>(),
                null);

            await pool._remoteSubpool.RegisterWorkerAsync(
                new WorkerAddRequest
                {
                    DisplayName = "Test Client 1",
                    UniqueId = "1",
                    Client = worker1Client,
                });

            await using var reservation1 = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);
            await using var reservation2 = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);
            await using var reservation3 = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);
            await using var reservation4 = await pool.ReserveCoreAsync(false, new CancellationTokenSource(5000).Token);

            Assert.Equal(4, server1.Reserved);

            await Task.Delay(1000);

            Assert.Equal(0, server1.Reserved);
        }
    }
}