namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Implementations;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;

    /// <summary>
    /// The Calico for Windows kubeconfig generating component generates the
    /// kubeconfig that is used by Calico on Windows nodes to communicate with
    /// the cluster. Since this requires creating a token in Kubernetes, it can't
    /// be done until the API server is ready.
    /// 
    /// Once it is done, it will raise the <see cref="WellKnownFlags.CalicoWindowsKubeConfigReady"/>.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class CalicoKubeConfigGeneratingComponent : IComponent
    {
        private readonly ICalicoKubeConfigGenerator _calicoWindowsKubeConfigGenerator;
        private readonly IPathProvider _pathProvider;
        private readonly IWslTranslation _wslTranslation;

        public CalicoKubeConfigGeneratingComponent(
            ICalicoKubeConfigGenerator calicoWindowsKubeConfigGenerator,
            IPathProvider pathProvider,
            IWslTranslation wslTranslation)
        {
            _calicoWindowsKubeConfigGenerator = calicoWindowsKubeConfigGenerator;
            _pathProvider = pathProvider;
            _wslTranslation = wslTranslation;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for the Kubernetes API server to be available.
            var kubernetesContext = await context.WaitForFlagAsync<KubernetesClientContextData>(WellKnownFlags.KubeApiServerReady);

            // Generate the kubeconfig if needed.
            var calicoKubeConfig = await _calicoWindowsKubeConfigGenerator.ProvisionCalicoKubeConfigIfNeededAsync(kubernetesContext.Kubernetes, cancellationToken);
            if (OperatingSystem.IsWindows())
            {
                // On the Windows controller, we are also going to be running a Windows node, so we need to write out the version of the
                // Calico kubeconfig with the controller address for our own calico-node process.
                var kubeconfigsPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs");
                await File.WriteAllTextAsync(
                    Path.Combine(kubeconfigsPath, "components", "component-calico-windows-node.kubeconfig"),
                    calicoKubeConfig.Replace("__CONTROLLER_ADDRESS__", (await _wslTranslation.GetTranslatedIPAddress(cancellationToken)).ToString(), StringComparison.Ordinal),
                    cancellationToken);
            }

            // Calico for Windows kubeconfig is now ready on disk.
            context.SetFlag(WellKnownFlags.CalicoWindowsKubeConfigReady, null);
        }
    }
}
