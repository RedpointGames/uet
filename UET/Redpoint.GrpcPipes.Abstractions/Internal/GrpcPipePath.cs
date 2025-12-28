namespace Redpoint.GrpcPipes.Abstractions.Internal
{
    using System;
    using System.Security.AccessControl;
    using System.Security.Principal;

    internal static class GrpcPipePath
    {
        private static string GetUserPipePath(string pipeName)
        {
            var grpcUserPipePathOverride = Environment.GetEnvironmentVariable("GRPC_PIPE_PATH_USER");
            if (!string.IsNullOrWhiteSpace(grpcUserPipePathOverride))
            {
                return grpcUserPipePathOverride;
            }

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

        private static string GetComputerPipePath(string pipeName)
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

        /// <summary>
        /// Given the pipe name and namespace, computes the path to the pointer file on disk.
        /// </summary>
        /// <remarks>
        /// You should not use this class in application code.
        /// </remarks>
        /// <param name="pipeName">The pipe name.</param>
        /// <param name="pipeNamespace">The pipe namespace.</param>
        /// <returns>The path to the pointer file on disk. This path may not exist yet.</returns>
        public static string GetPipePath(string pipeName, GrpcPipeNamespace pipeNamespace)
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

        /// <summary>
        /// Creates a directory path to store a pointer file, and configures its permissions.
        /// </summary>
        /// <remarks>
        /// You should not use this class in application code.
        /// </remarks>
        /// <param name="directoryPath">The directory path.</param>
        /// <param name="pipeNamespace">The pipe namespace.</param>
        public static void CreateDirectoryWithPermissions(
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
    }
}
