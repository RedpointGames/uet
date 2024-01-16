namespace Redpoint.GrpcPipes.Transport.Tcp
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes.Abstractions.Internal;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;

    /// <summary>
    /// Provides a gRPC pipe factory which uses TCP for transport.
    /// </summary>
    public sealed class TcpGrpcPipeFactory : IGrpcPipeFactory
    {
        private readonly IServiceProvider? _serviceProvider;

        /// <summary>
        /// Construct a new gRPC pipe factory which uses TCP for transport.
        /// </summary>
        /// <param name="serviceProvider">The service provider, which is optional when creating clients.</param>
        public TcpGrpcPipeFactory(IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        T IGrpcPipeFactory.CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<CallInvoker, T> constructor,
            GrpcChannelOptions? grpcChannelOptions)
        {
            var logger = _serviceProvider?.GetService<ILogger<TcpGrpcPipeFactory>>();

            var pipePath = GrpcPipePath.GetPipePath(pipeName, pipeNamespace);

            if (!File.Exists(pipePath))
            {
                logger?.LogTrace($"Pipe does not exist at '{pipePath}', returning dead gRPC channel.");

                return constructor(new TcpGrpcDeadClientCallInvoker());
            }

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

            return constructor(new TcpGrpcClientCallInvoker(IPEndPoint.Parse(pointer), logger));
        }

        IGrpcPipeServer<T> IGrpcPipeFactory.CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            T instance)
        {
            var pipePath = GrpcPipePath.GetPipePath(pipeName, pipeNamespace);
            GrpcPipePath.CreateDirectoryWithPermissions(Path.GetDirectoryName(pipePath)!, pipeNamespace);
            return new TcpGrpcPipeServer<T>(
                _serviceProvider!.GetRequiredService<ILogger<TcpGrpcPipeServer<T>>>(),
                pipePath,
                instance,
                pipeNamespace);
        }
    }
}
