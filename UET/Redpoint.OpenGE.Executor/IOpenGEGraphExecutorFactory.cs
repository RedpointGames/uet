namespace Redpoint.OpenGE.Executor
{
    public interface IOpenGEGraphExecutorFactory
    {
        IOpenGEGraphExecutor CreateGraphExecutor(
            Stream xgeJobXml,
            Dictionary<string, string> environmentVariables,
            bool turnOffExtraLogInfo = false,
            string? buildLogPrefix = null);
    }
}
