namespace Redpoint.GrpcPipes
{
    using Grpc.Core;

    public interface IGrpcPipeFactory
    {
        IGrpcPipeServer CreateServer(string pipeName);

        T CreateClient<T>(string pipeName, Func<CallInvoker, T> constructor);
    }
}