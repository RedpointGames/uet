namespace Redpoint.OpenGE.PreprocessorCache
{
    using Microsoft.Extensions.DependencyInjection;

    public static class PreprocessorCacheServiceExtensions
    {
        public static void AddOpenGEPreprocessorCache(this IServiceCollection services)
        {
            services.AddSingleton<OnDiskPreprocessorScanner, OnDiskPreprocessorScanner>();
            services.AddSingleton<ICachingPreprocessorScannerFactory, DefaultCachingPreprocessorScannerFactory>();
            services.AddSingleton<IPreprocessorCacheFactory, DefaultPreprocessorCacheFactory>();
            services.AddSingleton<IPreprocessorResolver, DefaultPreprocessorResolver>();
        }
    }
}
