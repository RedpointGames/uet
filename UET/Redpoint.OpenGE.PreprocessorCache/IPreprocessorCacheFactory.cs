namespace Redpoint.OpenGE.PreprocessorCache
{
    using Redpoint.ProcessExecution;

    public interface IPreprocessorCacheFactory
    {
        IPreprocessorCache CreatePreprocessorCache(ProcessSpecification daemonLaunchSpecification);
    }
}
