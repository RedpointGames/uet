namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using System.Runtime.Versioning;

    /// <summary>
    /// The swap disabling component turns off swap on Linux machines.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal class SwapDisablingComponent : IComponent
    {
        private readonly ILogger<SwapDisablingComponent> _logger;
        private readonly IProcessMonitorFactory _processMonitorFactory;

        public SwapDisablingComponent(
            ILogger<SwapDisablingComponent> logger,
            IProcessMonitorFactory processMonitorFactory)
        {
            _logger = logger;
            _processMonitorFactory = processMonitorFactory;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (OperatingSystem.IsLinux())
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Try to turn off swap, and if we can't, stop RKM.
            var disableSwap = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                filename: "/usr/sbin/swapoff",
                arguments: new[]
                {
                    "-a"
                }));
            if (await disableSwap.RunAsync(cancellationToken) != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not disable swap, which is required for Kubelet to run.");
                context.StopOnCriticalError();
                return;
            }
        }
    }
}
