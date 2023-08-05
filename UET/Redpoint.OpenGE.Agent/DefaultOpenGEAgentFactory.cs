namespace Redpoint.OpenGE.Agent
{
    using System.Threading.Tasks;

    internal class DefaultOpenGEAgentFactory : IOpenGEAgentFactory
    {
        public IOpenGEAgent CreateAgent(string rootPath)
        {
            return new DefaultOpenGEAgent(rootPath);
        }
    }

    internal class DefaultOpenGEAgent : IOpenGEAgent
    {
        private readonly string _rootPath;

        public DefaultOpenGEAgent(string rootPath)
        {
            _rootPath = rootPath;
        }

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}