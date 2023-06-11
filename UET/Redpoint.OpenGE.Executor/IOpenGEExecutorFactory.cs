namespace Redpoint.OpenGE.Executor
{
    public interface IOpenGEExecutorFactory
    {
        IOpenGEExecutor CreateExecutor(Stream xgeJobXml, bool turnOffExtraLogInfo = false, string? buildLogPrefix = null);
    }
}
