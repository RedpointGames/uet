namespace Redpoint.GrpcPipes.Transport.Tcp.Tests
{
    using Grpc.Core;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl.Tests;
    using System.Net;
    using System.Net.Sockets;
    using Xunit;

    public class TcpGrpcClientStreaming : TcpGrpcTestBase
    {
        public TcpGrpcClientStreaming(ITestOutputHelper output) : base(output)
        {
        }

        const int _timeoutThresholdDeadlineSeconds = 5;
        const int _timeoutThresholdAfterDeadlineSeconds = 10;

        [Fact]
        public async Task Call()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));
            var call = client.ClientStreaming(deadline: DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));
            await call.RequestStream.WriteAsync(new Request { Value = 1 });
            await call.RequestStream.WriteAsync(new Request { Value = 2 });
            await call.RequestStream.WriteAsync(new Request { Value = 3 });
            await call.RequestStream.CompleteAsync();
            var response = await call.ResponseAsync;

            Assert.Equal(6, response.Value);
        }

        [Fact]
        public async Task CallRepeated()
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
                var call = client.ClientStreaming(deadline: deadline);
                await call.RequestStream.WriteAsync(new Request { Value = 1 });
                await call.RequestStream.WriteAsync(new Request { Value = 2 });
                await call.RequestStream.WriteAsync(new Request { Value = 3 });
                await call.RequestStream.CompleteAsync();
                var response = await call.ResponseAsync;
                Assert.Equal(6, response.Value);
            }
        }

        [Fact]
        public async Task CallParallel()
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
                    var call = client.ClientStreaming(deadline: deadline);
                    await call.RequestStream.WriteAsync(new Request { Value = x, DelayMilliseconds = Random.Shared.Next(0, 30) });
                    await call.RequestStream.WriteAsync(new Request { Value = 2, DelayMilliseconds = Random.Shared.Next(0, 30) });
                    await call.RequestStream.WriteAsync(new Request { Value = 3, DelayMilliseconds = Random.Shared.Next(0, 30) });
                    await call.RequestStream.CompleteAsync();
                    var response = await call.ResponseAsync;
                    Assert.Equal(x + 5, response.Value);
                });
        }

        [Fact(Skip = "Unreliable when run on GitHub Actions")]
        public async Task CallWithDeadline()
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

            var call = client.ClientStreaming(deadline: deadline);
            await call.RequestStream.WriteAsync(new Request { Value = 1, DelayMilliseconds = 1000 });
            await call.RequestStream.WriteAsync(new Request { Value = 2, DelayMilliseconds = 2000 });
            // @note: The exception does not fire here or in CompleteAsync, since the client isn't waiting
            // for the server side delays before sending more data.
            await call.RequestStream.WriteAsync(new Request { Value = 3, DelayMilliseconds = 3000 });
            await call.RequestStream.CompleteAsync();
            var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                var response = await call.ResponseAsync;
            });
            Assert.True(ex.StatusCode == StatusCode.DeadlineExceeded || ex.StatusCode == StatusCode.Cancelled, "Expected StatusCode to be DeadlineExceeded or Cancelled.");
            Assert.True(service.CancellationTokenRaisedException, "Expected server to see cancellation.");
        }

        [Fact(Skip = "Unreliable when run on GitHub Actions")]
        public async Task CallWithCancellationToken()
        {
            var logger = GetLogger();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            await using var server = new TcpGrpcServer(listener, logger);
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            var service = new TcpGrpcProtocolService();
            TestService.BindService(server, service);

            using var cts = new CancellationTokenSource();
            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint, logger));

            var call = client.ClientStreaming(cancellationToken: cts.Token);
            await call.RequestStream.WriteAsync(new Request { Value = 1, DelayMilliseconds = 1000 });
            await call.RequestStream.WriteAsync(new Request { Value = 2, DelayMilliseconds = 2000 });
            cts.Cancel();

            // Make sure we observe our own cancellation when calling WriteAsync.
            var tex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await call.RequestStream.WriteAsync(new Request { Value = 3, DelayMilliseconds = 3000 });
            });

            // Make sure we observe our own cancellation when calling CompleteAsync.
            tex = await Assert.ThrowsAnyAsync<OperationCanceledException>(call.RequestStream.CompleteAsync);

            // Make sure the server observes our cancellation.
            var rex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                var response = await call.ResponseAsync;
            });
            Assert.Equal(StatusCode.Cancelled, rex.StatusCode);
            Assert.True(service.CancellationTokenRaisedException, "Expected server to see cancellation.");
        }

        [Fact]
        public async Task CallWithHeaders()
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
            var call = client.ClientStreaming(
                headers: requestHeaders,
                deadline: DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));
            await call.RequestStream.WriteAsync(new Request { Value = 3874 });
            await call.RequestStream.WriteAsync(new Request { Value = 200 });
            await call.RequestStream.CompleteAsync();

            var responseHeaders = await call.ResponseHeadersAsync;
            Assert.Equal("hello world", responseHeaders.Get("header")?.Value);

            var response = await call.ResponseAsync;
            Assert.Equal(4074, response.Value);

            var responseTrailers = call.GetTrailers();
            Assert.Equal("foo bar", responseTrailers.Get("trailer")?.Value);
        }
    }
}
