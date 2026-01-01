namespace Redpoint.GrpcPipes.Transport.Tcp.Tests
{
    using Grpc.Core;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl.Tests;
    using System.Net;
    using System.Net.Sockets;
    using Xunit;

    public class TcpGrpcServerStreaming : TcpGrpcTestBase
    {
        public TcpGrpcServerStreaming(ITestOutputHelper output) : base(output)
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
            var call = client.ServerStreaming(new Request { Value = 3 }, deadline: DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
            Assert.Equal(1, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
            Assert.Equal(2, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 3");
            Assert.Equal(3, call.ResponseStream.Current.Value);
            Assert.False(await call.ResponseStream.MoveNext(), "Expected end of server stream");
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
                var call = client.ServerStreaming(new Request { Value = 3 }, deadline: deadline);
                Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
                Assert.Equal(1, call.ResponseStream.Current.Value);
                Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
                Assert.Equal(2, call.ResponseStream.Current.Value);
                Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 3");
                Assert.Equal(3, call.ResponseStream.Current.Value);
                Assert.False(await call.ResponseStream.MoveNext(), "Expected end of server stream");
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
                    var call = client.ServerStreaming(new Request { Value = 3 }, deadline: deadline);
                    Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
                    Assert.Equal(1, call.ResponseStream.Current.Value);
                    Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
                    Assert.Equal(2, call.ResponseStream.Current.Value);
                    Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 3");
                    Assert.Equal(3, call.ResponseStream.Current.Value);
                    Assert.False(await call.ResponseStream.MoveNext(), "Expected end of server stream");
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

            var call = client.ServerStreaming(new Request { Value = 3, DelayMilliseconds = 2000 }, deadline: deadline);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
            Assert.Equal(1, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
            Assert.Equal(2, call.ResponseStream.Current.Value);
            var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await call.ResponseStream.MoveNext();
            });
            Assert.True(ex.StatusCode == StatusCode.DeadlineExceeded || ex.StatusCode == StatusCode.Cancelled, "Expected StatusCode to be DeadlineExceeded or Cancelled.");
            Assert.True(service.CancellationTokenRaisedException, "Expected server to see cancellation.");
        }

        [Fact]
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

            var call = client.ServerStreaming(new Request { Value = 3, DelayMilliseconds = 1000 }, cancellationToken: cts.Token);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
            Assert.Equal(1, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
            Assert.Equal(2, call.ResponseStream.Current.Value);
            cts.Cancel();
            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await call.ResponseStream.MoveNext();
            });
            await Task.Delay(1000);
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
            var call = client.ServerStreaming(
                new Request { Value = 3 },
                requestHeaders,
                DateTime.UtcNow.AddSeconds(_timeoutThresholdDeadlineSeconds));

            var responseHeaders = await call.ResponseHeadersAsync;
            Assert.Equal("hello world", responseHeaders.Get("header")?.Value);

            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 1");
            Assert.Equal(1, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 2");
            Assert.Equal(2, call.ResponseStream.Current.Value);
            Assert.True(await call.ResponseStream.MoveNext(), "Expected a response with Value = 3");
            Assert.Equal(3, call.ResponseStream.Current.Value);
            Assert.False(await call.ResponseStream.MoveNext(), "Expected end of server stream");

            var responseTrailers = call.GetTrailers();
            Assert.Equal("foo bar", responseTrailers.Get("trailer")?.Value);
        }
    }
}
