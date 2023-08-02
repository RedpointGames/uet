namespace Redpoint.OpenGE.Component.PreprocessorCache.OnDemand
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.DependencyResolution;
    using Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using System.Collections.Concurrent;

    internal class DefaultPreprocessorCacheFactory : IPreprocessorCacheFactory, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentBag<IDisposable> _caches;

        public DefaultPreprocessorCacheFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _caches = new ConcurrentBag<IDisposable>();
        }

        public IPreprocessorCache CreateOnDemandCache(ProcessSpecification daemonLaunchSpecification)
        {
            var cache = new OnDemandClientPreprocessorCache(
                _serviceProvider.GetRequiredService<ILogger<OnDemandClientPreprocessorCache>>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                _serviceProvider.GetRequiredService<IProcessExecutor>(),
                daemonLaunchSpecification);
            _caches.Add(cache);
            return cache;
        }

        public AbstractInProcessPreprocessorCache CreateInProcessCache()
        {
            return new InProcessPreprocessorCache(
                _serviceProvider.GetRequiredService<ICachingPreprocessorScannerFactory>(),
                _serviceProvider.GetRequiredService<IPreprocessorResolver>(),
                _serviceProvider.GetRequiredService<IReservationManagerFactory>());
        }

        public void Dispose()
        {
            foreach (var scanner in _caches)
            {
                scanner.Dispose();
            }
        }
    }
}
