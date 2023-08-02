namespace Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor
{
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;

    internal class OnDemandPreprocessorCacheAccessor : IPreprocessorCacheAccessor
    {
        private readonly IPreprocessorCacheFactory _onDemand;
        private readonly IPreprocessorOnDemandProcessSpecificationProvider _provider;

        public OnDemandPreprocessorCacheAccessor(
            IPreprocessorCacheFactory onDemand,
            IPreprocessorOnDemandProcessSpecificationProvider provider) 
        {
            _onDemand = onDemand;
            _provider = provider;
        }

        public IPreprocessorCache GetPreprocessorCache()
        {
            return _onDemand.CreateOnDemand(
                _provider.OnDemandProcessSpecification);
        }
    }
}
