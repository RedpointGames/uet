namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos"), SupportedOSPlatform("linux")]
    internal class UnixGrpcPipeFactory : IGrpcPipeFactory
    {
        internal readonly string _basePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ".redpoint-grpc-sockets");

        public IGrpcPipeServer CreateServer(string pipeName)
        {
            var server = new Server
            {
                Ports = { new ServerPort($"unix://{_basePath}/{pipeName}", 0, ServerCredentials.Insecure) },
            };
            return new UnixGrpcPipeServer(server);
        }

        public T CreateClient<T>(string pipeName, Func<CallInvoker, T> constructor)
        {
            var channel = new Channel($"unix://{_basePath}/{pipeName}:0", ChannelCredentials.Insecure);
            var callInvoker = new DefaultCallInvoker(channel);
            return constructor(callInvoker);
        }
    }
}