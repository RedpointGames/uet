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

        public override async Task<Response> ClientStreaming(IAsyncStreamReader<Request> requestStream, ServerCallContext context)
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

            var sum = 0;

            while (await requestStream.MoveNext())
            {
                sum += requestStream.Current.Value;

                if (requestStream.Current.DelayMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(requestStream.Current.DelayMilliseconds, context.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        CancellationTokenRaisedException = true;
                        throw;
                    }
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
                Value = sum,
            };
        }

        public override async Task ServerStreaming(Request request, IServerStreamWriter<Response> responseStream, ServerCallContext context)
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

            for (int i = 0; i < request.Value; i++)
            {
                await responseStream.WriteAsync(new Response
                {
                    Value = i + 1,
                }, context.CancellationToken);
            }

            if (context.RequestHeaders.Get("trailer") != null)
            {
                context.ResponseTrailers.Add(
                    "trailer",
                    context.RequestHeaders.Get("trailer")!.Value);
            }
        }

        public override async Task DuplexStreaming(IAsyncStreamReader<Request> requestStream, IServerStreamWriter<Response> responseStream, ServerCallContext context)
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

            while (await requestStream.MoveNext())
            {
                if (requestStream.Current.DelayMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(requestStream.Current.DelayMilliseconds, context.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        CancellationTokenRaisedException = true;
                        throw;
                    }
                }

                await responseStream.WriteAsync(new Response
                {
                    Value = requestStream.Current.Value,
                }, context.CancellationToken);
            }

            if (context.RequestHeaders.Get("trailer") != null)
            {
                context.ResponseTrailers.Add(
                    "trailer",
                    context.RequestHeaders.Get("trailer")!.Value);
            }
        }
    }
}
