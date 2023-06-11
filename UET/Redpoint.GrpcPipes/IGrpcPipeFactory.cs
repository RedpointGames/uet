namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;

    public interface IGrpcPipeFactory
    {
        IGrpcPipeServer<T> CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(string pipeName, T instance) where T : class;

        T CreateClient<T>(string pipeName, Func<GrpcChannel, T> constructor);
    }
}