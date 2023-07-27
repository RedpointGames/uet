namespace Redpoint.OpenGE.Executor
{
    public interface IOpenGEGraphExecutorFactory
    {
        IOpenGEGraphExecutor CreateGraphExecutor(Stream xgeJobXml, bool turnOffExtraLogInfo = false, string? buildLogPrefix = null);
    }
}
