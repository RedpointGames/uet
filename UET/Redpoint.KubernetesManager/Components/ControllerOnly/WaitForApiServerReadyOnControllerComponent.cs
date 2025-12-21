namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using k8s;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Components;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Wsl;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The Kubernetes client component tries to connect to the API server until it
    /// succeeds.
    /// </summary>
    internal class WaitForApiServerReadyOnControllerComponent : IComponent
    {
        private readonly IPathProvider _pathProvider;
        private readonly IKubeconfigGenerator _kubeconfigGenerator;
        private readonly ILogger<WaitForApiServerReadyOnControllerComponent> _logger;

        public WaitForApiServerReadyOnControllerComponent(
            IPathProvider pathProvider,
            IKubeconfigGenerator kubeconfigGenerator,
            ILogger<WaitForApiServerReadyOnControllerComponent> logger)
        {
            _pathProvider = pathProvider;
            _kubeconfigGenerator = kubeconfigGenerator;
            _logger = logger;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task<IKubernetes?> ConnectToClusterAsync(string configFile, int maximumWaitSeconds, CancellationToken cancellationToken)
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

        public async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for the user-admin kubeconfig to be provisioned on disk.
            await context.WaitForFlagAsync(WellKnownFlags.KubeconfigsReady);

            // Use the administrative user's kubeconfig for downstream authentication.
            var kubeconfigPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", "users", "user-admin.kubeconfig");
            var kubeconfigData = await File.ReadAllTextAsync(
                kubeconfigPath,
                cancellationToken);

            // Connect to the API server when it's ready.
            _logger.LogInformation("Waiting for Kubernetes API server to be up...");
            var kubernetes = await ConnectToClusterAsync(
                Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", "users", "user-admin.kubeconfig"),
                30,
                cancellationToken);
            if (kubernetes == null)
            {
                _logger.LogCritical("rkm is exiting because it failed to connect to Kubernetes API server within 30 seconds of startup. This usually means the Kubernetes server could not start properly. Without the Kubernetes API server running, rkm can't provision the RBAC roles required for clients to connect.");
                context.StopOnCriticalError();
                return;
            }

            context.SetFlag(WellKnownFlags.KubeApiServerReady, new KubernetesClientContextData(kubernetes, kubeconfigData));
        }
    }
}
