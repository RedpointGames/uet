namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Components;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The Kubernetes scheduler component sets up and runs the kube-scheduler process.
    /// </summary>
    internal class KubernetesSchedulerComponent : IComponent
    {
        private readonly ILogger<KubernetesSchedulerComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;
        private readonly IKubeConfigManager _kubeConfigManager;

        public KubernetesSchedulerComponent(
            ILogger<KubernetesSchedulerComponent> logger,
            IPathProvider pathProvider,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation,
            IKubeConfigManager kubeConfigManager)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
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
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            // Not a strict dependency, but doesn't make sense to start running this process until the API server is also started.
            await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerStarted);

            _logger.LogInformation("Starting kube-controller-manager and keeping it running...");
            var kubeMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "kubernetes-server", "kubernetes", "server", "bin", "kube-scheduler")),
                arguments: new[]
                {
                    $"--kubeconfig={_wslTranslation.TranslatePath(_kubeConfigManager.GetKubeconfigPath("components", "component-kube-scheduler"))}",
                    $"--v=2"
                },
                wsl: true));
            await kubeMonitor.RunAsync(cancellationToken);
        }
    }
}
