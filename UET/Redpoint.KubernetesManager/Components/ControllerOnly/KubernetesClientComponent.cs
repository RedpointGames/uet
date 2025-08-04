namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Components;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The Kubernetes client component tries to connect to the API server until it
    /// succeeds.
    /// </summary>
    internal class KubernetesClientComponent : IComponent
    {
        private readonly ILogger<KubernetesClientComponent> _logger;
        private readonly IKubernetesClientFactory _kubernetesClientFactory;
        private readonly IKubeConfigManager _kubeConfigManager;

        public KubernetesClientComponent(
            ILogger<KubernetesClientComponent> logger,
            IKubernetesClientFactory kubernetesClientFactory,
            IKubeConfigManager kubeConfigManager)
        {
            _logger = logger;
            _kubernetesClientFactory = kubernetesClientFactory;
            _kubeConfigManager = kubeConfigManager;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        public async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Connect to the API server when it's ready.
            _logger.LogInformation("Waiting for Kubernetes API server to be up...");
            var kubernetes = await _kubernetesClientFactory.ConnectToClusterAsync(
                _kubeConfigManager.GetKubeconfigPath("users", "user-admin"),
                30,
                cancellationToken);
            if (kubernetes == null)
            {
                _logger.LogCritical("rkm is exiting because it failed to connect to Kubernetes API server within 30 seconds of startup. This usually means the Kubernetes server could not start properly. Without the Kubernetes API server running, rkm can't provision the RBAC roles required for clients to connect.");
                context.StopOnCriticalError();
                return;
            }

            context.SetFlag(WellKnownFlags.KubeApiServerReady, new KubernetesClientContextData(kubernetes));
        }
    }
}
