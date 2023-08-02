namespace Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;

    internal class DefaultCachingPreprocessorScannerFactory : ICachingPreprocessorScannerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultCachingPreprocessorScannerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICachingPreprocessorScanner CreateCachingPreprocessorScanner(string dataDirectory)
        {
            return new CachingPreprocessorScanner(
                _serviceProvider.GetRequiredService<ILogger<CachingPreprocessorScanner>>(),
                _serviceProvider.GetRequiredService<OnDiskPreprocessorScanner>(),
                dataDirectory);
        }
    }
}
