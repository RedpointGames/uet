namespace Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor
{
    using Redpoint.ProcessExecution;

    internal interface IPreprocessorOnDemandProcessSpecificationProvider
    {
        ProcessSpecification OnDemandProcessSpecification { get; }
    }
}
