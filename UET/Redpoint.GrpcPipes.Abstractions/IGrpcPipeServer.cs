namespace Redpoint.GrpcPipes
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Wraps a gRPC server instance, allowing you to start and stop the server.
    /// </summary>
    /// <typeparam name="T">The gRPC server type.</typeparam>
    public interface IGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IAsyncDisposable where T : class
    {
        /// <summary>
        /// Starts the gRPC server.
        /// </summary>
        /// <returns>The awaitable task.</returns>
        Task StartAsync();

        /// <summary>
        /// Stops the gRPC server.
        /// </summary>
        /// <returns>The awaitable task.</returns>
        Task StopAsync();

        /// <summary>
        /// If this server is created from <see cref="IGrpcPipeFactory.CreateNetworkServer{T}(T)"/>, this is the local port on which the server is listening.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if this server is not created by <see cref="IGrpcPipeFactory.CreateNetworkServer{T}(T)"/>.</exception>
        int NetworkPort { get; }
    }
}