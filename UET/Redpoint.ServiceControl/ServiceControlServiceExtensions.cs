namespace Redpoint.ServiceControl
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Runtime.Versioning;

    /// <summary>
    /// Registers the <see cref="IServiceControl"/> service with dependency injection.
    /// </summary>
    public static class ServiceControlServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IServiceControl"/> service with dependency injection.
        /// </summary>
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("linux")]
        public static void AddServiceControl(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IServiceControl, WindowsServiceControl>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IServiceControl, MacServiceControl>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IServiceControl, LinuxServiceControl>();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
