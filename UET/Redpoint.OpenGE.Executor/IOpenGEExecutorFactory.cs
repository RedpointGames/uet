namespace Redpoint.OpenGE.Executor
{
    using Redpoint.OpenGE.Executor.BuildSetData;

    public interface IOpenGEExecutorFactory
    {
        IOpenGEExecutor CreateExecutor(Stream xgeJobXml, bool turnOffExtraLogInfo = false, string? buildLogPrefix = null);
    }
}
