namespace Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor
{
    using Redpoint.OpenGE.Component.PreprocessorCache;

    internal class InProcessPreprocessorCacheAccessor : IPreprocessorCacheAccessor
    {
        private readonly IPreprocessorCache _preprocessorCache;

        public InProcessPreprocessorCacheAccessor(
            IPreprocessorCache preprocessorCache)
        {
            _preprocessorCache = preprocessorCache;
        }

        public IPreprocessorCache GetPreprocessorCache()
        {
            return _preprocessorCache;
        }
    }
}
