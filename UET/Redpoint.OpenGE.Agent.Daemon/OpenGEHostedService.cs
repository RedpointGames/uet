namespace Redpoint.OpenGE.Agent.Daemon
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using System.Threading;
    using System.Threading.Tasks;

    internal class OpenGEHostedService : IHostedService, IPreprocessorCacheAccessor
    {
        private readonly ILogger<OpenGEHostedService> _logger;
        private readonly IOpenGEAgentFactory _openGEAgentFactory;
        private bool _running;
        private IOpenGEAgent? _agent;

        public OpenGEHostedService(
            ILogger<OpenGEHostedService> logger,
            IOpenGEAgentFactory openGEAgentFactory)
        {
            _logger = logger;
            _openGEAgentFactory = openGEAgentFactory;
        }

        public Task<IPreprocessorCache> GetPreprocessorCacheAsync()
        {
            return _agent!.GetPreprocessorCacheAsync();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _agent = _openGEAgentFactory.CreateAgent(true);
            await _agent.StartAsync();
            _logger.LogInformation("The OpenGE system-wide agent is now running.");
            _running = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_running)
            {
                await _agent!.StopAsync();
                _logger.LogInformation("The OpenGE system-wide agent has stopped.");
                _running = false;
            }
        }
    }
}
