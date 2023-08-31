namespace Redpoint.OpenGE.Component.PreprocessorCache.OnDemand
{
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.ProcessExecution;

    public interface IPreprocessorCacheFactory
    {
        IPreprocessorCache CreateOnDemandCache(ProcessSpecification daemonLaunchSpecification);

        AbstractInProcessPreprocessorCache CreateInProcessCache();
    }
}
