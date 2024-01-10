namespace Redpoint.GrpcPipes.Transport.Tcp.Tests
{
    using global::Grpc.Core;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl.Tests;

    internal class TcpGrpcProtocolService : TestService.TestServiceBase
    {
        public bool CancellationTokenRaisedException = false;

        public override async Task<Response> Unary(
            Request request,
            ServerCallContext context)
        {
            if (context.RequestHeaders.Get("header") != null)
            {
                var responseHeaders = new Metadata
                {
                    {
                        "header",
                        context.RequestHeaders.Get("header")!.Value
                    }
                };
                await context.WriteResponseHeadersAsync(responseHeaders);
            }

            if (request.DelayMilliseconds > 0)
            {
                try
                {
                    await Task.Delay(request.DelayMilliseconds, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancellationTokenRaisedException = true;
                    throw;
                }
            }

            if (context.RequestHeaders.Get("trailer") != null)
            {
                context.ResponseTrailers.Add(
                    "trailer",
                    context.RequestHeaders.Get("trailer")!.Value);
            }

            return new Response
            {
                Value = request.Value
            };
        }
    }
}
