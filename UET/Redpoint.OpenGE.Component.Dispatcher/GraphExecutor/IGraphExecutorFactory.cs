namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    public interface IGraphExecutorFactory
    {
        IGraphExecutor CreateGraphExecutor(
            Stream xgeJobXml,
            Dictionary<string, string> environmentVariables,
            string workingDirectory,
            string buildNodeName);
    }
}
