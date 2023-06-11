namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using GrpcDotNetNamedPipes;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal class WindowsGrpcPipeServer : IGrpcPipeServer
    {
        private readonly NamedPipeServer _server;

        public WindowsGrpcPipeServer(NamedPipeServer namedPipeServer)
        {
            _server = namedPipeServer;
        }

        public void AddService(
            Action<ServiceBinderBase> bindToServiceBinder,
            Func<ServerServiceDefinition> returnServiceDefinition)
        {
            bindToServiceBinder(_server.ServiceBinder);
        }

        public void Start()
        {
            _server.Start();
        }

        public Task StopAsync()
        {
            _server.Kill();
            return Task.CompletedTask;
        }
    }
}