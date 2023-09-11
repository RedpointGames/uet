namespace Redpoint.Uet.Core.Permissions
{
    using Microsoft.Extensions.Logging;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading.Tasks;

    internal sealed class DefaultWorldPermissionApplier : IWorldPermissionApplier
    {
        private readonly ILogger<DefaultWorldPermissionApplier> _logger;

        [SupportedOSPlatform("windows")]
        private static readonly Lazy<SecurityIdentifier> _everyoneWindows = new(() => new SecurityIdentifier(WellKnownSidType.WorldSid, null));

        public DefaultWorldPermissionApplier(
            ILogger<DefaultWorldPermissionApplier> logger)
        {
            _logger = logger;
        }

        public async ValueTask GrantEveryonePermissionAsync(string path, CancellationToken cancellationToken)
        {
            if (File.Exists(path))
            {
                await GrantEveryonePermissionToFileAsync(new FileInfo(path), cancellationToken);
            }
            else if (Directory.Exists(path))
            {
                await GrantEveryonePermissionToDirectoryAsync(new DirectoryInfo(path), cancellationToken);
            }
        }

        private ValueTask GrantEveryonePermissionToFileAsync(FileInfo file, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var dacl = file.GetAccessControl(AccessControlSections.Access);
                    dacl.AddAccessRule(new FileSystemAccessRule(_everyoneWindows.Value, FileSystemRights.FullControl, AccessControlType.Allow));
                    file.SetAccessControl(dacl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to grant Everyone full control of '{file.FullName}'. Exception was: {ex}");
                }
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                try
                {
                    var mode = file.UnixFileMode;
                    mode |= UnixFileMode.UserRead;
                    mode |= UnixFileMode.UserWrite;
                    mode |= UnixFileMode.GroupRead;
                    mode |= UnixFileMode.GroupWrite;
                    mode |= UnixFileMode.OtherRead;
                    mode |= UnixFileMode.OtherWrite;
                    file.UnixFileMode = mode;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to grant ugo+rw on '{file.FullName}'. Exception was: {ex}");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }

        private async ValueTask GrantEveryonePermissionToDirectoryAsync(DirectoryInfo directory, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var dacl = directory.GetAccessControl(AccessControlSections.Access);
                    dacl.AddAccessRule(new FileSystemAccessRule(_everyoneWindows.Value, FileSystemRights.FullControl, AccessControlType.Allow));
                    directory.SetAccessControl(dacl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to grant Everyone full control of '{directory.FullName}'. Exception was: {ex}");
                }
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                try
                {
                    var mode = directory.UnixFileMode;
                    mode |= UnixFileMode.UserRead;
                    mode |= UnixFileMode.UserWrite;
                    mode |= UnixFileMode.GroupRead;
                    mode |= UnixFileMode.GroupWrite;
                    mode |= UnixFileMode.OtherRead;
                    mode |= UnixFileMode.OtherWrite;
                    directory.UnixFileMode = mode;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to grant ugo+rw on '{directory.FullName}'. Exception was: {ex}");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            await Parallel.ForEachAsync(
                directory.GetFiles().ToAsyncEnumerable(),
                GrantEveryonePermissionToFileAsync);

            await Parallel.ForEachAsync(
                directory.GetDirectories().ToAsyncEnumerable(),
                GrantEveryonePermissionToDirectoryAsync);
        }
    }
}
