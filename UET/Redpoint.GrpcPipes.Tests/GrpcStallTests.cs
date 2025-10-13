namespace Redpoint.GrpcPipes.Tests
{
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes.Transport.Tcp;
    using System.Diagnostics;
    using TestPipes;
    using static TestPipes.TestService;

    public class GrpcStallTests
    {
        private readonly ITestOutputHelper _output;

        public GrpcStallTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private class TestServiceServer : TestServiceBase
        {
            public override async Task<TestResponse> TestMethod(TestRequest request, Grpc.Core.ServerCallContext context)
            {
                await Task.Delay(3000);
                return new TestResponse();
            }

            public override async Task TestStreamingMethod(TestRequest request, IServerStreamWriter<TestResponse> responseStream, ServerCallContext context)
            {
                for (var i = 0; i < 3; i++)
                {
                    await Task.Delay(2000);
                    await responseStream.WriteAsync(new TestResponse());
                }
            }
        }

        [Fact(Skip = "This test should only be run manually (and doesn't reproduce the issue we were trying to catch).")]
        public async Task TestPipeDoesNotStall()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_output);
            });
            services.AddGrpcPipes<TcpGrpcPipeFactory>();

            var sp = services.BuildServiceProvider();
            var pipeFactory = sp.GetRequiredService<IGrpcPipeFactory>();

            var pipeName = $"test-grpc-stall-{Environment.ProcessId}";

            // Set up the server.
            var testService = new TestServiceServer();
            var server = pipeFactory.CreateServer(
                pipeName,
                GrpcPipeNamespace.Computer,
                testService);
            await server.StartAsync();
            try
            {
                // Idle for 1 minute.
                //await Task.Delay(60000);

                // Make the client.
                var client = pipeFactory.CreateClient(
                    pipeName,
                    GrpcPipeNamespace.Computer,
                    channel => new TestServiceClient(channel));

                // See if we can make a request.
                var response = await client.TestMethodAsync(new TestRequest(), deadline: DateTime.UtcNow.AddSeconds(5));
                Assert.NotNull(response);

                for (int i = 0; i < 3; i++)
                {
                    // See if we can make a request with a short deadline.
                    await Assert.ThrowsAsync<RpcException>(async () =>
                    {
                        await client.TestMethodAsync(new TestRequest(), deadline: DateTime.UtcNow.AddSeconds(1));
                    });

                    // Can we still make requests?
                    response = await client.TestMethodAsync(new TestRequest(), deadline: DateTime.UtcNow.AddSeconds(5));
                    Assert.NotNull(response);
                }

                for (int i = 0; i < 3; i++)
                {
                    // See if we can make a streaming request with a short deadline.
                    await Assert.ThrowsAsync<RpcException>(async () =>
                    {
                        var streamingResponse = client.TestStreamingMethod(new TestRequest(), deadline: DateTime.UtcNow.AddSeconds(1));
                        while (await streamingResponse.ResponseStream.MoveNext())
                        {
                            _ = streamingResponse.ResponseStream.Current;
                        }
                    });

                    // Can we still make requests?
                    var streamingResponse = client.TestStreamingMethod(new TestRequest(), deadline: DateTime.UtcNow.AddSeconds(10));
                    while (await streamingResponse.ResponseStream.MoveNext())
                    {
                        _ = streamingResponse.ResponseStream.Current;
                    }
                }
            }
            finally
            {
                await server.StopAsync();
            }
        }
    }
}