namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using GrpcDotNetNamedPipes;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal class WindowsGrpcPipeFactory : IGrpcPipeFactory
    {
        public IGrpcPipeServer CreateServer(string pipeName)
        {
            return new WindowsGrpcPipeServer(new NamedPipeServer(pipeName));
        }

        public T CreateClient<T>(string pipeName, Func<CallInvoker, T> constructor)
        {
            var channel = new NamedPipeChannel(".", pipeName);
            return constructor(channel);
        }
    }
}