namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;

    /// <summary>
    /// The networking configuration component uses the <see cref="INetworkingConfiguration"/>
    /// interface to prepare the current machine's networking for Kubernetes.
    /// </summary>
    internal class NetworkingConfigurationComponent : IComponent
    {
        private readonly INetworkingConfiguration _networkingConfiguration;
        private readonly IWindowsFeatureManager _windowsFeatureManager;

        public NetworkingConfigurationComponent(
            INetworkingConfiguration networkingConfiguration,
            IWindowsFeatureManager windowsFeatureManager)
        {
            _networkingConfiguration = networkingConfiguration;
            _windowsFeatureManager = windowsFeatureManager;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
            {
                // @todo: Probably should be in it's own component, but works for now.
                await _windowsFeatureManager.EnsureRequiredFeaturesAreInstalled(context.Role == RoleType.Controller, cancellationToken);
            }

            // Try to set up networking, and if we can't, stop RKM.
            if (!await _networkingConfiguration.ConfigureForKubernetesAsync(context.Role == RoleType.Controller, cancellationToken))
            {
                context.StopOnCriticalError();
                return;
            }

            context.SetFlag(WellKnownFlags.OSNetworkingReady);
        }
    }
}
