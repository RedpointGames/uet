﻿namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Provides methods for constructing gRPC pipe servers and clients.
    /// </summary>
    public interface IGrpcPipeFactory
    {
        /// <summary>
        /// Constructs a gRPC server that offers services on the given pipe.
        /// </summary>
        /// <typeparam name="T">The type of the gRPC server.</typeparam>
        /// <param name="pipeName">The name of the pipe to serve on.</param>
        /// <param name="pipeNamespace">The namespace of the pipe.</param>
        /// <param name="instance">The instance of the gRPC server to respond to requests.</param>
        /// <returns>The <see cref="IGrpcPipeServer{T}"/> that wraps the gRPC server instance. Allows you to start and stop serving as needed.</returns>
        IGrpcPipeServer<T> CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            T instance) where T : class;

        /// <summary>
        /// Creates a gRPC client that connects to the given pipe.
        /// </summary>
        /// <typeparam name="T">The gRPC client type.</typeparam>
        /// <param name="pipeName">The name of the pipe to connect to.</param>
        /// <param name="pipeNamespace">The namespace of the pipe.</param>
        /// <param name="constructor">The callback to construct the client type using the provided channel.</param>
        /// <param name="grpcChannelOptions">Additional options to apply to the channel.</param>
        /// <returns>The constructor gRPC client.</returns>
        T CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<GrpcChannel, T> constructor,
            GrpcChannelOptions? grpcChannelOptions = null);
    }
}