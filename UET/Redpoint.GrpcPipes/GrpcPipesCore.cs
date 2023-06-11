namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;

    public static class GrpcPipesCore
    {
        public static T CreateClient<T>(string pipeName, Func<GrpcChannel, T> constructor)
        {
            return new AspNetGrpcPipeFactory().CreateClient(pipeName, constructor);
        }
    }
}