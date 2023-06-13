namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;

    /// <summary>
    /// Provides the <see cref="CreateClient{T}(string, GrpcPipeNamespace, Func{Grpc.Net.Client.GrpcChannel, T})"/> method for consumers that don't want to use dependency injection.
    /// </summary>
    public static class GrpcPipesCore
    {
        /// <summary>
        /// Creates a gRPC client that connects to the given pipe.
        /// </summary>
        /// <typeparam name="T">The gRPC client type.</typeparam>
        /// <param name="pipeName">The name of the pipe to connect to.</param>
        /// <param name="pipeNamespace">The namespace of the pipe.</param>
        /// <param name="constructor">The callback to construct the client type using the provided channel.</param>
        /// <returns>The constructor gRPC client.</returns>
        public static T CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<GrpcChannel, T> constructor)
        {
            return new AspNetGrpcPipeFactory(null).CreateClient(pipeName, pipeNamespace, constructor);
        }
    }
}