namespace Redpoint.Vfs.Driver.WinFsp
{
    using Fsp;
    using Redpoint.Vfs.Abstractions;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class WinFspVfsDriver : IVfsDriver
    {
        private readonly FileSystemHost _host;
        private readonly IVfsLayer _projectionLayer;

        public WinFspVfsDriver(FileSystemHost host, IVfsLayer projectionLayer)
        {
            _host = host;
            _projectionLayer = projectionLayer;
        }

        public void Dispose()
        {
            _host.Unmount();
            _host.Dispose();
            _projectionLayer.Dispose();
        }
    }
}
