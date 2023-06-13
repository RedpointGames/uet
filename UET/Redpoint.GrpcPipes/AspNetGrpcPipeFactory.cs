namespace Redpoint.GrpcPipes
{
    using Grpc.Net.Client;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;
    using System.Security.AccessControl;
    using System.Security.Principal;

    internal class AspNetGrpcPipeFactory : IGrpcPipeFactory
    {
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
            if (File.Exists(pipePath))
            {
                // Remove the existing pipe. Newer servers always take over from older ones.
                File.Delete(pipePath);
            }
            return new AspNetGrpcPipeServer<T>(pipePath, instance);
        }

        public T CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<GrpcChannel, T> constructor)
        {
            var socketsHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var pipePath = GetPipePath(pipeName, pipeNamespace);
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

            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = socketsHandler,
            });

            return constructor(channel);
        }
    }
}