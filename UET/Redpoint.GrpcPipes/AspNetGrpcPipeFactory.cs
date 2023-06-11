namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;

    internal class AspNetGrpcPipeFactory : IGrpcPipeFactory
    {
        internal readonly string _basePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ".redpoint-grpc-sockets");

        public IGrpcPipeServer<T> CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(string pipeName, T instance) where T : class
        {
            return new AspNetGrpcPipeServer<T>(Path.Combine(_basePath, pipeName), instance);
        }

        public T CreateClient<T>(string pipeName, Func<GrpcChannel, T> constructor)
        {
            var socketsHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(
                        AddressFamily.Unix,
                        SocketType.Stream,
                        ProtocolType.Unspecified);
                    await socket.ConnectAsync(
                        new UnixDomainSocketEndPoint(Path.Combine(_basePath, pipeName)),
                        cancellationToken);
                    return new NetworkStream(socket, true);
                }
            };

            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = socketsHandler,
            });

            return constructor(channel);
        }
    }
}