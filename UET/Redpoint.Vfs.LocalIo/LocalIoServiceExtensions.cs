namespace Redpoint.Vfs.LocalIo
{
    using Microsoft.Extensions.DependencyInjection;

    public static class LocalIoServiceExtensions
    {
        public static void AddLocalIoFileFactory(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<ILocalIoVfsFileFactory, WindowsLocalIoVfsFileFactory>();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
