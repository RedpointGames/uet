namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;

    /// <summary>
    /// Provides methods for constructing gRPC pipe servers and clients.
    /// </summary>
    public interface IGrpcPipeFactory
    {
        /// <summary>
        /// Constructs the factory without dependency injection.
        /// </summary>
        static virtual IGrpcPipeFactory CreateFactoryWithoutInjection()
        {
            throw new NotImplementedException();
        }

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
            Func<CallInvoker, T> constructor,
            GrpcChannelOptions? grpcChannelOptions = null);

        /// <summary>
        /// Constructs a gRPC server that offers services on the loopback adapter or local network.
        /// </summary>
        /// <typeparam name="T">The type of the gRPC server.</typeparam>
        /// <param name="instance">The instance of the gRPC server to respond to requests.</param>
        /// <param name="loopbackOnly">If true, the server listens only on the loopback interface.</param>
        /// <returns>The <see cref="IGrpcPipeServer{T}"/> that wraps the gRPC server instance. Allows you to start and stop serving as needed.</returns>
        IGrpcPipeServer<T> CreateNetworkServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(
            T instance, bool loopbackOnly = false) where T : class;

        /// <summary>
        /// Creates a gRPC client that connects to services on the local network.
        /// </summary>
        /// <typeparam name="T">The gRPC client type.</typeparam>
        /// <param name="endpoint">The remote endpoint to connect to.</param>
        /// <param name="constructor">The callback to construct the client type using the provided channel.</param>
        /// <param name="grpcChannelOptions">Additional options to apply to the channel.</param>
        /// <returns>The constructor gRPC client.</returns>
        T CreateNetworkClient<T>(
            IPEndPoint endpoint,
            Func<CallInvoker, T> constructor,
            GrpcChannelOptions? grpcChannelOptions = null);
    }
}