namespace Redpoint.OpenGE.Agent
{
    public interface IOpenGEAgentFactory
    {
        IOpenGEAgent CreateAgent(string rootPath);
    }
}