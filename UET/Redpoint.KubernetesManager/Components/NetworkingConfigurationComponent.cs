namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.Extensions.Logging;
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
        private readonly ILogger<NetworkingConfigurationComponent> _logger;

        public NetworkingConfigurationComponent(
            INetworkingConfiguration networkingConfiguration,
            IWindowsFeatureManager windowsFeatureManager,
            ILogger<NetworkingConfigurationComponent> logger)
        {
            _networkingConfiguration = networkingConfiguration;
            _windowsFeatureManager = windowsFeatureManager;
            _logger = logger;
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
                _logger.LogInformation("Ensuring features are installed on Windows...");

                // Run the Windows Feature checks in the background in case the DISM API stops responding.
                _ = Task.Run(async () => await _windowsFeatureManager.EnsureRequiredFeaturesAreInstalled(context.Role == RoleType.Controller, cancellationToken), cancellationToken);
            }

            // Try to set up networking, and if we can't, stop RKM.
            _logger.LogInformation("Configuring networking...");
            if (!await _networkingConfiguration.ConfigureForKubernetesAsync(context.Role == RoleType.Controller, cancellationToken))
            {
                context.StopOnCriticalError();
                return;
            }

            context.SetFlag(WellKnownFlags.OSNetworkingReady);
        }
    }
}
