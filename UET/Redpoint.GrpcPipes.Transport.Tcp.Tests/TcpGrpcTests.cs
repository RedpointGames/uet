namespace Redpoint.GrpcPipes.Transport.Tcp.Tests
{
    using global::Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl.Tests;
    using System.Net;
    using System.Net.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    public class TcpGrpcTests
    {
        private readonly ITestOutputHelper _output;

        public TcpGrpcTests(ITestOutputHelper output)
        {
            _output = output;
        }

        const int _timeoutThresholdBeforeDeadlineSeconds = 2;
        const int _timeoutThresholdDeadlineSeconds = 5;
        const int _timeoutThresholdAfterDeadlineSeconds = 10;

        private ILogger GetLogger()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                if (Environment.GetEnvironmentVariable("CI") != "true")
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddXUnit(
                        _output,
                        configure =>
                        {
                        });
                }
            });
            return services.BuildServiceProvider().GetRequiredService<ILogger<TcpGrpcTests>>();
        }

        [Fact]
        public async Task UnaryCall()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));
            var response = await client.UnaryAsync(new Request { Value = 1 }, deadline: DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));

            Assert.Equal(1, response.Value);
        }

        [Fact]
        public async Task UnaryCallRepeated()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var deadline = DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds);
            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));

            for (int i = 0; i < 10; i++)
            {
                var response = await client.UnaryAsync(new Request { Value = 1 }, deadline: deadline);
                Assert.Equal(1, response.Value);
            }
        }

        [Fact]
        public async Task UnaryCallParallel()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var deadline = DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds);
            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));

            await Parallel.ForEachAsync(
                Enumerable.Range(1, 100).ToAsyncEnumerable(),
                new ParallelOptions { MaxDegreeOfParallelism = 20 },
                async (x, _) =>
                {
                    var response = await client.UnaryAsync(new Request { Value = x, DelayMilliseconds = Random.Shared.Next(0, 100) }, deadline: deadline);
                    Assert.Equal(x, response.Value);
                });
        }

        [Fact]
        public async Task UnaryCallWithDeadline()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            var service = new TcpGrpcProtocolService();
            TestService.BindService(server, service);

            var deadline = DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds);
            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));

            var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                var response = await client.UnaryAsync(new Request { Value = 1, DelayMilliseconds = _timeoutThresholdAfterDeadlineSeconds * 1000 }, deadline: deadline);
            });
            Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
            Assert.True(service.CancellationTokenRaisedException, "Expected server to see cancellation.");
        }

        [Fact]
        public async Task UnaryCallWithCancellationToken()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            var service = new TcpGrpcProtocolService();
            TestService.BindService(server, service);

            using var cts = new CancellationTokenSource(_timeoutThresholdDeadlineSeconds * 1000);
            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));

            var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                var response = await client.UnaryAsync(new Request { Value = 1, DelayMilliseconds = _timeoutThresholdAfterDeadlineSeconds * 1000 }, cancellationToken: cts.Token);
            });
            Assert.Equal(StatusCode.Cancelled, ex.StatusCode);
            Assert.True(service.CancellationTokenRaisedException, "Expected server to see cancellation.");
        }

        [Fact]
        public async Task UnaryCallWithHeaders()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var requestHeaders = new Metadata
            {
                { "header", "hello world" },
                { "trailer", "foo bar" }
            };

            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));
            var call = client.UnaryAsync(
                new Request { Value = 3874 },
                requestHeaders,
                DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));

            var responseHeaders = await call.ResponseHeadersAsync;
            Assert.Equal("hello world", responseHeaders.Get("header")?.Value);

            var response = await call.ResponseAsync;
            Assert.Equal(3874, response.Value);

            var responseTrailers = call.GetTrailers();
            Assert.Equal("foo bar", responseTrailers.Get("trailer")?.Value);
        }
    }
}
