namespace Redpoint.KubernetesManager.Components.WslExtra
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The WSL containerd component sets up and runs a second containerd instance
    /// inside WSL, specifically on Windows controllers where we need Linux pods.
    /// </summary>
    internal class WslContainerdComponent : IComponent
    {
        private readonly ILogger<WslContainerdComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IResourceManager _resourceManager;
        private readonly IWslTranslation _wslTranslation;
        private readonly IProcessMonitorFactory _processMonitorFactory;

        public WslContainerdComponent(
            ILogger<WslContainerdComponent> logger,
            IPathProvider pathProvider,
            IResourceManager resourceManager,
            IWslTranslation wslTranslation,
            IProcessMonitorFactory processMonitorFactory)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _resourceManager = resourceManager;
            _wslTranslation = wslTranslation;
            _processMonitorFactory = processMonitorFactory;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller && OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
                // @todo: Do we need to unmount stuff in WSL as well?
                // context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            _logger.LogInformation("Setting up WSL containerd configuration...");
            await _resourceManager.ExtractResource(
                "containerd-config-linux.toml",
                Path.Combine(_pathProvider.RKMRoot, "wsl", "containerd-state", "config.toml"),
                new Dictionary<string, string>
                {
                    // containerd can't store stuff on the Windows filesystem, so we need to use a location in /run
                    { "__CONTAINERD_ROOT__", $"/run/{_pathProvider.RKMInstallationId}-containerd-state" },
                    { "__RUNC_ROOT__", _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc")) },
                    { "__CNI_PLUGINS_ROOT__", _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "cni-plugins")) }
                });

            _logger.LogInformation("Starting WSL containerd and keeping it running...");
            var containerdMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "containerd", "bin", "containerd")),
                arguments: new[]
                {
                    "--config",
                    _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "containerd-state", "config.toml")),
                    "--log-level",
                    "debug"
                },
                wsl: true));
            // @note: If we want to do cleanup in response to a Stopping signal we'll probably
            // need to change this cancellation token so that the containerd process doesn't stop
            // until we've had a chance to stop all containers via ctr.
            await containerdMonitor.RunAsync(cancellationToken);
        }
    }
}
