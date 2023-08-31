namespace Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor
{
    using Redpoint.OpenGE.Component.PreprocessorCache;

    public interface IPreprocessorCacheAccessor
    {
        Task<IPreprocessorCache> GetPreprocessorCacheAsync();
    }
}
