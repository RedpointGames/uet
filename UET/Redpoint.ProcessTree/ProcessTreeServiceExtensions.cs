namespace Redpoint.ProcessTree
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    /// <summary>
    /// Registers the <see cref="IProcessTree"/> service with dependency injection.
    /// </summary>
    public static class ProcessTreeServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IProcessTree"/> service with dependency injection.
        /// </summary>
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("linux")]
        public static void AddProcessTree(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IProcessTree, WindowsProcessTree>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IProcessTree, MacProcessTree>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IProcessTree, LinuxProcessTree>();
            }
            else
            {
                throw new PlatformNotSupportedException("Redpoint.ProcessTree only works on Windows, macOS and Linux.");
            }
        }
    }
}