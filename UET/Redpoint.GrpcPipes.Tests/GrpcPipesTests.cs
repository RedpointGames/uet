namespace Redpoint.GrpcPipes.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Diagnostics;
    using TestPipes;
    using static TestPipes.TestService;

    public class GrpcPipesTests
    {
        private class TestServiceServer : TestServiceBase
        {
            private readonly Action _methodCalled;

            public TestServiceServer(Action methodCalled)
            {
                _methodCalled = methodCalled;
            }

            public override Task<TestResponse> TestMethod(TestRequest request, Grpc.Core.ServerCallContext context)
            {
                _methodCalled();
                return Task.FromResult(new TestResponse());
            }
        }

        [Fact]
        public async Task TestUserPipes()
        {
            var services = new ServiceCollection();
            services.AddGrpcPipes();

            var sp = services.BuildServiceProvider();
            var pipeFactory = sp.GetRequiredService<IGrpcPipeFactory>();

            var pipeName = $"test-grpc-pipes-{Process.GetCurrentProcess().Id}";

            var isCalled = false;
            var testService = new TestServiceServer(() => isCalled = true);

            var server = pipeFactory.CreateServer(
                pipeName,
                GrpcPipeNamespace.User,
                testService);
            await server.StartAsync();

            var client = pipeFactory.CreateClient(
                pipeName,
                GrpcPipeNamespace.User,
                channel => new TestServiceClient(channel));

            await client.TestMethodAsync(new TestRequest());

            await server.StopAsync();

            Assert.True(isCalled, "Expected TestMethod to be called");
        }

        [Fact]
        public async Task TestComputerPipes()
        {
            var services = new ServiceCollection();
            services.AddGrpcPipes();

            var sp = services.BuildServiceProvider();
            var pipeFactory = sp.GetRequiredService<IGrpcPipeFactory>();

            var pipeName = $"test-grpc-pipes-{Process.GetCurrentProcess().Id}";

            var isCalled = false;
            var testService = new TestServiceServer(() => isCalled = true);

            var server = pipeFactory.CreateServer(
                pipeName,
                GrpcPipeNamespace.Computer,
                testService);
            await server.StartAsync();

            var client = pipeFactory.CreateClient(
                pipeName,
                GrpcPipeNamespace.Computer,
                channel => new TestServiceClient(channel));

            await client.TestMethodAsync(new TestRequest());

            await server.StopAsync();

            Assert.True(isCalled, "Expected TestMethod to be called");
        }
    }
}