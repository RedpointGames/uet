namespace Redpoint.UET.BuildPipeline.Executors
{
    public interface IBuildExecutorFactory
    {
        IBuildExecutor CreateExecutor();
    }
}
