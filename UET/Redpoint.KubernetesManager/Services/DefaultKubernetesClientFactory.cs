namespace Redpoint.KubernetesManager.Services
{
    using k8s;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    internal class DefaultKubernetesClientFactory : IKubernetesClientFactory
    {
        private readonly ILogger<DefaultKubernetesClientFactory> _logger;

        public DefaultKubernetesClientFactory(ILogger<DefaultKubernetesClientFactory> logger)
        {
            _logger = logger;
        }

        public async Task<IKubernetes?> ConnectToClusterAsync(string configFile, int maximumWaitSeconds, CancellationToken cancellationToken)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(configFile);

            var kubernetes = new Kubernetes(config);

            for (var i = 0; i < maximumWaitSeconds && !cancellationToken.IsCancellationRequested; i++)
            {
                try
                {
                    var code = await kubernetes.CoreV1.ListNamespaceAsync(cancellationToken: cancellationToken);
                    // _logger.LogInformation($"Connected to API server, Kubernetes is running version: {code.Major}.{code.Minor}");
                    return kubernetes;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning($"Failed to connect to Kubernetes API server; it might still be starting up: {ex}");
                    if (i < maximumWaitSeconds - 1)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogError("Failed to connect to Kubernetes API server. Check the process logs!");
            return null;
        }
    }
}
