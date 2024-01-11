namespace Redpoint.Grpc.Transport.Tcp.Tests
{
    using global::Grpc.Core;
    using System.Net;
    using System.Net.Sockets;
    using Xunit;

    public class TcpGrpcTests
    {
        [Fact]
        public async Task UnaryCallWorks()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            var server = new TcpGrpcServer(listener);
            listener.Start();
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint));
            var response = await client.UnaryAsync(new Request { Value = 1 });

            Assert.Equal(1, response.Value);
        }

        [Fact]
        public async Task UnaryCallWithHeadersWorks()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

            var listener = new TcpListener(endpoint);
            var server = new TcpGrpcServer(listener);
            listener.Start();
            endpoint = (IPEndPoint)listener.LocalEndpoint;
            TestService.BindService(server, new TcpGrpcProtocolService());

            var requestHeaders = new Metadata
            {
                { "header", "hello world" },
                { "trailer", "foo bar" }
            };

            var client = new TestService.TestServiceClient(new TcpGrpcClientCallInvoker(endpoint));
            var call = client.UnaryAsync(
                new Request { Value = 3874 },
                requestHeaders);

            var responseHeaders = await call.ResponseHeadersAsync;
            Assert.Equal("hello world", responseHeaders.Get("header")?.Value);

            var response = await call.ResponseAsync;
            Assert.Equal(3874, response.Value);

            var responseTrailers = call.GetTrailers();
            Assert.Equal("foo bar", responseTrailers.Get("trailer")?.Value);
        }
    }
}
