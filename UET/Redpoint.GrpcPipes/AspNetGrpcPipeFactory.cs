namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;

    internal class AspNetGrpcPipeFactory : IGrpcPipeFactory
    {
        private string GetBasePath()
        {
            if (OperatingSystem.IsWindows())
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    ".redpoint-grpc-pipes",
                    Environment.UserName);
                Directory.CreateDirectory(path);
                return path;
            }
            else if (OperatingSystem.IsMacOS())
            {
                var rootPath = "/Users/Shared/.redpoint-grpc-pipes";
                Directory.CreateDirectory(rootPath);
                File.SetUnixFileMode(
                    rootPath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead |
                    UnixFileMode.GroupWrite |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |
                    UnixFileMode.OtherWrite |
                    UnixFileMode.OtherExecute);
                var path = Path.Combine(
                    rootPath,
                    Environment.UserName);
                Directory.CreateDirectory(path);
                return path;
            }
            else if (OperatingSystem.IsLinux())
            {
                var rootPath = "/tmp/.redpoint-grpc-pipes";
                Directory.CreateDirectory(rootPath);
                File.SetUnixFileMode(
                    rootPath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead |
                    UnixFileMode.GroupWrite |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |
                    UnixFileMode.OtherWrite |
                    UnixFileMode.OtherExecute);
                var path = Path.Combine(
                    rootPath,
                    Environment.UserName);
                Directory.CreateDirectory(path);
                return path;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        public IGrpcPipeServer<T> CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(string pipeName, T instance) where T : class
        {
            return new AspNetGrpcPipeServer<T>(Path.Combine(GetBasePath(), pipeName), instance);
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
                        new UnixDomainSocketEndPoint(Path.Combine(GetBasePath(), pipeName)),
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