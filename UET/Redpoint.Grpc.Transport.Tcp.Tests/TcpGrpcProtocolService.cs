namespace Redpoint.Grpc.Transport.Tcp.Tests
{
    using global::Grpc.Core;

    internal class TcpGrpcProtocolService : TestService.TestServiceBase
    {
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
