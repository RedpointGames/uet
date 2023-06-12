namespace Redpoint.Vfs.Driver.WinFsp
{
    using Fsp;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class WinFspVfsDriverFactory : IVfsDriverFactory
    {
        private readonly ILogger<WinFspVfsDriverFactory> _factoryLogger;
        private readonly ILogger<WinFspVfsDriverImpl> _instanceLogger;

        public WinFspVfsDriverFactory(
            ILogger<WinFspVfsDriverFactory> factoryLogger,
            ILogger<WinFspVfsDriverImpl> instanceLogger)
        {
            _factoryLogger = factoryLogger;
            _instanceLogger = instanceLogger;
        }

        public IVfsDriver? InitializeAndMount(
            IVfsLayer projectionLayer,
            string mountPath,
            VfsDriverOptions? options)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                return null;
            }

            if (options?.DriverLogPath != null)
            {
                FileSystemHost.SetDebugLogFile(options?.DriverLogPath);
                _factoryLogger.LogInformation($"Emitting WinFsp debug logs to: {options?.DriverLogPath}");
            }

            bool isReturning = false;
            var fs = new WinFspVfsDriverImpl(
                _instanceLogger,
                projectionLayer,
                options as WinFspVfsDriverOptions ?? new WinFspVfsDriverOptions());
            var host = new FileSystemHost(fs);
            fs.FileSystemHost = host;
            try
            {
                if (Directory.Exists(mountPath))
                {
                    Directory.Delete(mountPath);
                }

                var mountResult = host.Mount(
                    $@"\\.\{mountPath}",
                    null,
                    false,
                    options?.DriverLogPath != null ? unchecked((uint)-1) : 0
                    );
                if (mountResult < 0)
                {
                    throw new InvalidOperationException($"Failed to mount WinFsp filesystem: 0x{mountResult:X}");
                }

                isReturning = true;
                return new WinFspVfsDriver(host, projectionLayer);
            }
            finally
            {
                if (!isReturning)
                {
                    host.Dispose();
                    projectionLayer.Dispose();
                }
            }
        }
    }
}
