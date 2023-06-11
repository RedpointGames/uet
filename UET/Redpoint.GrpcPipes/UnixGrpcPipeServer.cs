namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using System;

    internal class UnixGrpcPipeServer : IGrpcPipeServer
    {
        private Server _server;

        public UnixGrpcPipeServer(Server server)
        {
            _server = server;
        }

        public void AddService(
            Action<ServiceBinderBase> bindToServiceBinder,
            Func<ServerServiceDefinition> returnServiceDefinition)
        {
            _server.Services.Add(returnServiceDefinition());
        }

        public void Start()
        {
            _server.Start();
        }

        public Task StopAsync()
        {
            return _server.KillAsync();
        }
    }
}