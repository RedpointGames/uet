namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes.Abstractions.Internal;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;

    /// <summary>
    /// Provides a gRPC pipe factory which uses ASP.NET for transport.
    /// </summary>
    public sealed class AspNetGrpcPipeFactory : IGrpcPipeFactory
    {
        private readonly IServiceProvider? _serviceProvider;

        /// <summary>
        /// Construct a new gRPC pipe factory which uses ASP.NET for transport.
        /// </summary>
        /// <param name="serviceProvider">The service provider, which is optional when creating clients.</param>
        public AspNetGrpcPipeFactory(IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        IGrpcPipeServer<T> IGrpcPipeFactory.CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            T instance)
        {
            var pipePath = GrpcPipePath.GetPipePath(pipeName, pipeNamespace);
            GrpcPipePath.CreateDirectoryWithPermissions(Path.GetDirectoryName(pipePath)!, pipeNamespace);
            return new AspNetGrpcPipeServer<T>(
                _serviceProvider!.GetRequiredService<ILogger<AspNetGrpcPipeServer<T>>>(),
                pipePath,
                instance,
                pipeNamespace);
        }

        T IGrpcPipeFactory.CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<CallInvoker, T> constructor,
            GrpcChannelOptions? grpcChannelOptions)
        {
            var logger = _serviceProvider?.GetService<ILogger<AspNetGrpcPipeFactory>>();

            var pipePath = GrpcPipePath.GetPipePath(pipeName, pipeNamespace);

            if (!File.Exists(pipePath))
            {
                logger?.LogTrace($"Pipe does not exist at '{pipePath}', returning dead gRPC channel.");

                // We still have to return the client, but act as a "dead" channel. It should act the same
                // as if you created a gRPC client to an endpoint that is not responding (i.e. it should
                // create the client successfully, but calls should fail).
                var socketsHandler = new SocketsHttpHandler
                {
                    ConnectCallback = (_, cancellationToken) =>
                    {
                        // $"The gRPC pipe was not found at: {pipePath}"
                        throw new SocketException((int)SocketError.ConnectionRefused);
                    }
                };

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.HttpHandler = socketsHandler;

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                return constructor(GrpcChannel.ForAddress("http://localhost", options).CreateCallInvoker());
            }

            GrpcChannel channel;
            if (!OperatingSystem.IsWindows())
            {
                logger?.LogTrace($"Creating gRPC channel with UNIX socket at path: {pipePath}");

                var socketsHandler = new SocketsHttpHandler
                {
                    ConnectCallback = async (_, cancellationToken) =>
                    {
                        logger?.LogTrace($"Connecting to UNIX socket at path: {pipePath}");

                        var socket = new Socket(
                            AddressFamily.Unix,
                            SocketType.Stream,
                            ProtocolType.Unspecified);
                        await socket.ConnectAsync(
                            new UnixDomainSocketEndPoint(pipePath),
                            cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, true);
                    }
                };

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.HttpHandler = socketsHandler;

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                channel = GrpcChannel.ForAddress("http://localhost", options);
            }
            else
            {
                logger?.LogTrace($"Reading pointer file from path: {pipePath}");

                string pointerFileContent;
                using (var reader = new StreamReader(new FileStream(
                    pipePath,
                    FileMode.Open,
                    FileAccess.Read,
                    // @note: FileShare.Write is necessary here because the server still holds ReadWrite
                    // access, even though it won't be writing into the file after it starts.
                    FileShare.Read | FileShare.Write | FileShare.Delete)))
                {
                    pointerFileContent = reader.ReadToEnd().Trim();
                }
                if (!pointerFileContent.StartsWith("pointer: ", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Pointer file format is invalid!");
                }

                var pointer = pointerFileContent["pointer: ".Length..].Trim();

                logger?.LogTrace($"Creating gRPC channel with TCP socket from pointer file: {pointer}");

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.Credentials = ChannelCredentials.Insecure;

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                channel = GrpcChannel.ForAddress(pointer, options);
            }

            return constructor(channel.CreateCallInvoker());
        }
    }
}