namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The containerd component sets up and runs the containerd process.
    /// </summary>
    internal class ContainerdComponent : IComponent, IDisposable
    {
        private readonly ILogger<ContainerdComponent> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IPathProvider _pathProvider;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWindowsHcsService _hcsService;
        private readonly CancellationTokenSource _containerdReadyToShutdown;

        public ContainerdComponent(
            ILogger<ContainerdComponent> logger,
            IResourceManager resourceManager,
            IPathProvider pathProvider,
            IProcessMonitorFactory processMonitorFactory,
            IWindowsHcsService hcsService)
        {
            _logger = logger;
            _resourceManager = resourceManager;
            _pathProvider = pathProvider;
            _processMonitorFactory = processMonitorFactory;
            _hcsService = hcsService;
            _containerdReadyToShutdown = new CancellationTokenSource();
        }

        public void Dispose()
        {
            ((IDisposable)_containerdReadyToShutdown).Dispose();
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);

            _logger.LogInformation("Setting up containerd configuration...");
            if (OperatingSystem.IsLinux())
            {
                await _resourceManager.ExtractResource(
                    "containerd-config-linux.toml",
                    Path.Combine(_pathProvider.RKMRoot, "containerd-state", "config.toml"),
                    new Dictionary<string, string>
                    {
                        { "__CONTAINERD_ROOT__", Path.Combine(_pathProvider.RKMRoot, "containerd-state") },
                        { "__RUNC_ROOT__", Path.Combine(_pathProvider.RKMRoot, "runc") },
                        { "__CNI_PLUGINS_ROOT__", Path.Combine(_pathProvider.RKMRoot, "cni-plugins") }
                    });
            }
            else if (OperatingSystem.IsWindows())
            {
                await _resourceManager.ExtractResource(
                    "containerd-config-windows.toml",
                    Path.Combine(_pathProvider.RKMRoot, "containerd-state", "config.toml"),
                    new Dictionary<string, string>
                    {
                        { "__CONTAINERD_ROOT__", Path.Combine(_pathProvider.RKMRoot, "containerd-state").Replace("\\", "\\\\", StringComparison.Ordinal) },
                        { "__CNI_PLUGINS_ROOT__", Path.Combine(_pathProvider.RKMRoot, "cni-plugins").Replace("\\", "\\\\", StringComparison.Ordinal) }
                    });
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            _logger.LogInformation("Starting containerd and keeping it running...");
            var containerdMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "containerd", "bin", "containerd"),
                arguments: new[]
                {
                    "--config",
                    Path.Combine(_pathProvider.RKMRoot, "containerd-state", "config.toml"),
                    "--log-level",
                    "debug"
                }));

            // Now we run two tasks in parallel:
            // - One that watches containerd to exit, on our custom cancellation token
            // - Another which waits until kubelet has stopped and cleans up containers
            //   before telling containerd to actually exit.
            await Task.WhenAll(
                Task.Run(async () =>
                {
                    try
                    {
                        // Since we want to clean up containers, we don't shutdown containerd
                        // immediately on process stop. Instead we raise this cancellation
                        // token once we've finished our ctr calls.
                        await containerdMonitor.RunAsync(_containerdReadyToShutdown.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when containerd stops, and we don't want to
                        // prevent the second task from finishing up it's WaitForFlagAsync.
                    }
                    finally
                    {
                        context.SetFlag(WellKnownFlags.ContainerdStopped);
                    }
                }, CancellationToken.None),
                Task.Run(async () =>
                {
                    // Wait for the kubelet to stop.
                    await context.WaitForUninterruptableFlagAsync(WellKnownFlags.KubeletStopped);

                    // Use ctr to delete all of the containers.
                    _logger.LogInformation($"Fetching a list of containers to stop...");
                    var listProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(_pathProvider.RKMRoot, "containerd", "bin", "ctr"),
                        ArgumentList =
                        {
                            "--namespace",
                            "k8s.io",
                            "c",
                            "list"
                        },
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    });
                    var listLines = (await listProcess!.StandardOutput.ReadToEndAsync()).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                    var containerIds = new List<string>();
                    var containerRegex = new Regex("^([a-f0-9]+)\\s");
                    foreach (var line in listLines)
                    {
                        var match = containerRegex.Match(line);
                        if (match.Success)
                        {
                            containerIds.Add(match.Groups[1].Value);
                        }
                    }

                    _logger.LogInformation($"{containerIds.Count} containers to terminate.");
                    foreach (var containerId in containerIds)
                    {
                        _logger.LogInformation($"Deleting container: {containerId}");
                        var deleteProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(_pathProvider.RKMRoot, "containerd", "bin", "ctr"),
                            ArgumentList =
                            {
                                "--namespace",
                                "k8s.io",
                                "c",
                                "delete",
                                containerId,
                            },
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        await deleteProcess!.WaitForExitAsync();
                    }

                    _logger.LogInformation($"Containers have been terminated, now ready to shutdown containerd.");
                    _containerdReadyToShutdown.Cancel();

                    _logger.LogInformation($"Waiting for containerd to stop...");
                    await context.WaitForUninterruptableFlagAsync(WellKnownFlags.ContainerdStopped);
                    _logger.LogInformation($"containerd is stopped.");
                }, CancellationToken.None));
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsLinux())
            {
                // Unmount anything under the RKM root that appears in /etc/mtab, so that it's safe
                // to delete an RKM install without running into "resource busy".
                var mounts = await File.ReadAllLinesAsync("/etc/mtab", cancellationToken);
                foreach (var mount in mounts)
                {
                    var mountComponents = mount.Split(' ');
                    if (mountComponents.Length > 2 && mountComponents[1].StartsWith(_pathProvider.RKMRoot, StringComparison.Ordinal))
                    {
                        _logger.LogInformation($"Unmounting container folder due to shutdown: {mountComponents[1]}");

                        var unmount = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                            filename: "/usr/bin/umount",
                            arguments: new[]
                            {
                                mountComponents[1],
                            },
                            silent: true));
                        if ((await unmount.RunAsync(CancellationToken.None)) != 0)
                        {
                            _logger.LogWarning($"Unmount operation failed for: {mountComponents[1]}");
                        }
                    }
                }

                _logger.LogInformation($"Unmounted all container folders.");
            }
            else if (OperatingSystem.IsWindows())
            {
                foreach (var computeSystem in _hcsService.GetHcsComputeSystems())
                {
                    if (computeSystem.SystemType == "Container")
                    {
                        _logger.LogInformation($"Killing HCS compute system {computeSystem.Id}...");
                        _hcsService.TerminateHcsSystem(computeSystem.Id);
                    }
                }
            }
        }
    }
}
