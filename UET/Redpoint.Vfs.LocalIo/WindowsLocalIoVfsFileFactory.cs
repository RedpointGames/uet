namespace Redpoint.Vfs.LocalIo
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class WindowsLocalIoVfsFileFactory : ILocalIoVfsFileFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public WindowsLocalIoVfsFileFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IVfsFileHandle<IVfsFile> CreateOffsetVfsFileHandle(string path, long offset, long length)
        {
            return new WindowsOffsetVfsFile(
                _serviceProvider.GetRequiredService<ILogger<WindowsOffsetVfsFile>>(),
                path,
                offset,
                length);
        }

        public IVfsFileHandle<IVfsFile> CreateVfsFileHandle(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, IWindowsVfsFileCallbacks? callbacks, string? scratchPath)
        {
            return new WindowsVfsFile(
                _serviceProvider.GetRequiredService<ILogger<WindowsOffsetVfsFile>>(),
                path,
                fileMode,
                fileAccess,
                fileShare,
                callbacks,
                scratchPath);
        }
    }
}
