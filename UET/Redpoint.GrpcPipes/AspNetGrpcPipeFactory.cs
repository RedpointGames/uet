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
                        try
                        {
                            di.SetAccessControl(ac);
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
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
                instance,
                pipeNamespace);
        }

        public T CreateClient<T>(
            string pipeName,
            GrpcPipeNamespace pipeNamespace,
            Func<GrpcChannel, T> constructor,
            GrpcChannelOptions? grpcChannelOptions)
        {
            var logger = _serviceProvider?.GetService<ILogger<AspNetGrpcPipeFactory>>();

            var pipePath = GetPipePath(pipeName, pipeNamespace);

            if (!File.Exists(pipePath))
            {
                logger?.LogTrace($"Pipe does not exist at '{pipePath}', returning dead gRPC channel.");

                // We still have to return the client, but act as a "dead" channel. It should act the same
                // as if you created a gRPC client to an endpoint that is not responding (i.e. it should
                // create the client successfully, but calls should fail).
                var socketsHandler = new SocketsHttpHandler
                {
                    ConnectCallback = (_, cancellationToken) =>
                    {
                        // $"The gRPC pipe was not found at: {pipePath}"
                        throw new SocketException((int)SocketError.ConnectionRefused);
                    }
                };

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.HttpHandler = socketsHandler;

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                return constructor(GrpcChannel.ForAddress("http://localhost", options));
            }

            GrpcChannel channel;
            if (!OperatingSystem.IsWindows())
            {
                logger?.LogTrace($"Creating gRPC channel with UNIX socket at path: {pipePath}");

                var socketsHandler = new SocketsHttpHandler
                {
                    ConnectCallback = async (_, cancellationToken) =>
                    {
                        logger?.LogTrace($"Connecting to UNIX socket at path: {pipePath}");

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

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                channel = GrpcChannel.ForAddress("http://localhost", options);
            }
            else
            {
                logger?.LogTrace($"Reading pointer file from path: {pipePath}");

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

                var pointer = pointerFileContent.Substring("pointer: ".Length).Trim();

                logger?.LogTrace($"Creating gRPC channel with TCP socket from pointer file: {pointer}");

                var options = grpcChannelOptions ?? new GrpcChannelOptions();
                options.Credentials = ChannelCredentials.Insecure;

                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;

                channel = GrpcChannel.ForAddress(pointer, options);
            }

            return constructor(channel);
        }
    }
}