namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Component.PreprocessorCache.DependencyResolution;
    using Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner;
    using Redpoint.OpenGE.Component.PreprocessorCache.Filesystem;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;

    public static class PreprocessorCacheServiceExtensions
    {
        public static void AddOpenGEComponentPreprocessorCache(this IServiceCollection services)
        {
            services.AddSingleton<OnDiskPreprocessorScanner, OnDiskPreprocessorScanner>();
            services.AddSingleton<ICachingPreprocessorScannerFactory, DefaultCachingPreprocessorScannerFactory>();
            services.AddSingleton<IPreprocessorCacheFactory, DefaultPreprocessorCacheFactory>();
            services.AddSingleton<IPreprocessorResolver, DefaultPreprocessorResolver>();
            services.AddSingleton<IOpenGECacheReservationManagerProvider, OpenGECacheReservationManagerProvider>();
            if (OperatingSystem.IsWindowsVersionAtLeast(5, 2))
            {
                services.AddSingleton<IFilesystemExistenceProvider, WindowsZoneTreeFilesystemExistenceProvider>();
            }
            else
            {
                services.AddSingleton<IFilesystemExistenceProvider, GenericInMemoryFilesystemExistenceProvider>();
            }
        }
    }
}
