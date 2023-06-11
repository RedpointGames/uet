namespace Redpoint.GrpcPipes
{
    using Grpc.Core;

    public interface IGrpcPipeServer
    {
        void AddService(
            Action<ServiceBinderBase> bindToServiceBinder,
            Func<ServerServiceDefinition> returnServiceDefinition);

        void Start();

        Task StopAsync();
    }
}