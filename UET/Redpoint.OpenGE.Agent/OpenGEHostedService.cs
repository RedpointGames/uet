namespace Redpoint.OpenGE.Agent
{
    using Microsoft.Extensions.Hosting;
    using System.Threading;
    using System.Threading.Tasks;

    public class OpenGEHostedService : IHostedService
    {
        private readonly IOpenGEAgent _agent;

        public OpenGEHostedService(
            IOpenGEAgentFactory agentFactory)
        {
            string rootPath;
            if (OperatingSystem.IsWindows())
            {
                rootPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "OpenGE");
            }
            else if (OperatingSystem.IsMacOS())
            {
                rootPath = Path.Combine("/Users", "Shared", "OpenGE");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            _agent = agentFactory.CreateAgent(rootPath);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _agent.StartAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _agent.StopAsync();
        }
    }
}