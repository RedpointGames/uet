namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;
    using System.Security.AccessControl;
    using System.Security.Principal;

    internal class AspNetGrpcPipeFactory : IGrpcPipeFactory
    {
        private readonly IServiceProvider? _serviceProvider;

        public AspNetGrpcPipeFactory(
            IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private string GetUserPipePath(string pipeName)
        {
            if (OperatingSystem.IsWindows())
            {
                if (WindowsIdentity.GetCurrent().IsSystem)
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        ".grpc",
                        "sysuser",
                        pipeName);
                }
                else
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".grpc",
                        pipeName);
                }
            }
            else
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".grpc",
                    pipeName);
            }
        }

        private string GetComputerPipePath(string pipeName)
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    ".grpc",
                    pipeName);
            }
            else
            {
                return Path.Combine("/tmp/.grpc", pipeName);
            }
        }

        private string GetPipePath(string pipeName, GrpcPipeNamespace pipeNamespace)
        {
            if (pipeNamespace == GrpcPipeNamespace.User)
            {
                return GetUserPipePath(pipeName);
            }
            else
            {
                return GetComputerPipePath(pipeName);
            }
        }

        private void CreateDirectoryWithPermissions(
            string directoryPath,
            GrpcPipeNamespace pipeNamespace)
        {
            Directory.CreateDirectory(directoryPath);
            switch (pipeNamespace)
            {
                case GrpcPipeNamespace.User:
                    if (OperatingSystem.IsWindows())
                    {
                        // We don't need to do anything for this.
                    }
                    else
                    {
                        File.SetUnixFileMode(
                            directoryPath,
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute);
                    }
                    break;
                case GrpcPipeNamespace.Computer:
                    if (OperatingSystem.IsWindows())
                    {
                        var di = new DirectoryInfo(directoryPath);
                        var ac = di.GetAccessControl();
                        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                        ac.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.Modify | FileSystemRights.Synchronize, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                        di.SetAccessControl(ac);
                    }
                    else
                    {
                        File.SetUnixFileMode(
                            directoryPath,
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead |
                            UnixFileMode.GroupWrite |
                            UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead |
                            UnixFileMode.OtherWrite |
                            UnixFileMode.OtherExecute);
                    }
                    break;
            }
        }

        public IGrpcPipeServer<T> CreateServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            T instance) where T : class
        {
            var pipePath = GetPipePath(pipeName, pipeNamespace);
            CreateDirectoryWithPermissions(Path.GetDirectoryName(pipePath)!, pipeNamespace);
            return new AspNetGrpcPipeServer<T>(
                _serviceProvider!.GetRequiredService<ILogger<AspNetGrpcPipeServer<T>>>(),
                pipePath,
                instance);
        }

        public T CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<GrpcChannel, T> constructor,
            GrpcChannelOptions? grpcChannelOptions)
        {
            var pipePath = GetPipePath(pipeName, pipeNamespace);

            if (!File.Exists(pipePath))
            {
                throw new FileNotFoundException($"The gRPC pipe was not found at: {pipePath}", pipePath);
            }

            var isUnixSocket = false;
            if (OperatingSystem.IsWindows())
            {
                var attributes = File.GetAttributes(pipePath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // This is a Unix socket on Windows. Maintain compatibility with older versions.
                    isUnixSocket = true;
                }
            }
            else
            {
                isUnixSocket = true;
            }

            GrpcChannel channel;
            if (isUnixSocket)
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
                            new UnixDomainSocketEndPoint(pipePath),
                            cancellationToken);
                        return new NetworkStream(socket, true);
                    }
                };

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.HttpHandler = socketsHandler;

                channel = GrpcChannel.ForAddress("http://localhost", options);
            }
            else
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new InvalidOperationException("Did not expect pointer file on non-Windows system!");
                }

                string pointerFileContent;
                using (var reader = new StreamReader(new FileStream(
                    pipePath,
                    FileMode.Open,
                    FileAccess.Read,
                    // @note: FileShare.Write is necessary here because the server still holds ReadWrite
                    // access, even though it won't be writing into the file after it starts.
                    FileShare.Read | FileShare.Write | FileShare.Delete)))
                {
                    pointerFileContent = reader.ReadToEnd().Trim();
                }
                if (!pointerFileContent.StartsWith("pointer: "))
                {
                    throw new InvalidOperationException("Pointer file format is invalid!");
                }

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.Credentials = ChannelCredentials.Insecure;

                channel = GrpcChannel.ForAddress(pointerFileContent.Substring("pointer: ".Length).Trim(), options);
            }

            return constructor(channel);
        }
    }
}