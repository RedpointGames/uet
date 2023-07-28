namespace Redpoint.OpenGE.PreprocessorCache
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System.Collections.Concurrent;

    internal class DefaultPreprocessorCacheFactory : IPreprocessorCacheFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentBag<SystemWidePreprocessorCache> _caches;

        public DefaultPreprocessorCacheFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _caches = new ConcurrentBag<SystemWidePreprocessorCache>();
        }

        public IPreprocessorCache CreatePreprocessorCache(ProcessSpecification daemonLaunchSpecification)
        {
            var cache = new SystemWidePreprocessorCache(
                _serviceProvider.GetRequiredService<ILogger<SystemWidePreprocessorCache>>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                _serviceProvider.GetRequiredService<IProcessExecutor>(),
                daemonLaunchSpecification);
            _caches.Add(cache);
            return cache;
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
