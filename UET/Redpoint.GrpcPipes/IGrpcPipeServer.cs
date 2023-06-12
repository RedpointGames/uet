namespace Redpoint.GrpcPipes
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Wraps a gRPC server instance, allowing you to start and stop the server.
    /// </summary>
    /// <typeparam name="T">The gRPC server type.</typeparam>
    public interface IGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> where T : class
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
    }
}