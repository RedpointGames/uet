namespace Redpoint.Vfs.LocalIo
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers the <see cref="ILocalIoVfsFileFactory"/> implementation with dependency injection.
    /// </summary>
    public static class LocalIoServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="ILocalIoVfsFileFactory"/> implementation with dependency injection.
        /// </summary>
        public static void AddLocalIoFileFactory(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<ILocalIoVfsFileFactory, WindowsLocalIoVfsFileFactory>();
            }
        }
    }
}
