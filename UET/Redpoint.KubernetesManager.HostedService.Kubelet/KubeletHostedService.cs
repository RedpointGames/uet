namespace Redpoint.KubernetesManager.HostedService.Kubelet
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Concurrency;
    using Redpoint.IO;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.Manifest.Client;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.PerpetualProcess;
    using Redpoint.ProcessExecution;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal class KubeletHostedService : IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IGenericManifestClient _genericManifestClient;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly ILogger<KubeletHostedService> _logger;
        private readonly IProcessKiller _processKiller;
        private readonly IProcessExecutor _processExecutor;
        private readonly RunKubeletCommand.Options _options;
        private readonly Gate _kubeletStarted;

        private Task? _backgroundTask;

        public static ICommandInvocationContext? InvocationContext;

        public KubeletHostedService(
            IHostApplicationLifetime hostApplicationLifetime,
            IGenericManifestClient genericManifestClient,
            IProcessMonitorFactory processMonitorFactory,
            ILogger<KubeletHostedService> logger,
            IProcessKiller processKiller,
            IProcessExecutor processExecutor,
            RunKubeletCommand.Options options)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _genericManifestClient = genericManifestClient;
            _processMonitorFactory = processMonitorFactory;
            _logger = logger;
            _processKiller = processKiller;
            _processExecutor = processExecutor;
            _options = options;
            _kubeletStarted = new Gate();
        }

        private async Task RunWithManifestAsync(KubeletManifest manifest, CancellationToken cancellationToken)
        {
            // Log the version of kubelet that we're about to run.
            var versionWithSuffix = manifest.KubernetesVersion;
            _logger.LogInformation($"Received manifest; attempting to start kubelet '{versionWithSuffix}'...");

            // Check if kubelet is already installed.
            var kubeletInstallPath = Path.Combine(
                manifest.KubeletInstallRootPath,
                versionWithSuffix);
            if (!Directory.Exists(kubeletInstallPath) ||
                !File.Exists(Path.Combine(kubeletInstallPath, ".rkm-flag")))
            {
                _logger.LogInformation($"Downloading and installing kubelet '{versionWithSuffix}'...");

                // Erase any previous partial download/extract.
                if (Directory.Exists(kubeletInstallPath))
                {
                    await DirectoryAsync.DeleteAsync(kubeletInstallPath, true);
                }
                Directory.CreateDirectory(kubeletInstallPath);

                // Download kubelet.
                {
                    var platformName = OperatingSystem.IsWindows() ? "windows" : "linux";
                    var suffix = OperatingSystem.IsWindows() ? ".exe" : "";
                    var kubeletUri = new Uri($"https://dl.k8s.io/v{manifest.KubernetesVersion}/bin/{platformName}/amd64/kubelet{suffix}");
                    var kubeletPath = Path.Combine(kubeletInstallPath, $"kubelet{suffix}");
                    _logger.LogInformation($"Downloading kubelet binary from '{kubeletUri}'...");
                    using (var fileStream = new FileStream(kubeletPath + ".tmp", FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (var httpClient = new HttpClient())
                        {
                            using (var stream = await httpClient.GetStreamAsync(kubeletUri, cancellationToken))
                            {
                                await stream.CopyToAsync(fileStream, cancellationToken);
                            }
                        }
                    }
                    if (OperatingSystem.IsLinux())
                    {
                        File.SetUnixFileMode(
                            kubeletPath + ".tmp",
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    }
                    File.Move(kubeletPath + ".tmp", kubeletPath, true);
                    _logger.LogInformation($"Downloaded kubelet from '{kubeletUri}' to '{kubeletPath}'.");
                }

                // Mark this install as having been completed.
                File.WriteAllText(
                    Path.Combine(kubeletInstallPath, ".rkm-flag"),
                    "ok");
            }

            // Log that kubelet is now ready on disk.
            _logger.LogInformation($"Kubelet '{versionWithSuffix}' is now ready on disk.");

            // Create state directory for files.
            Directory.CreateDirectory(manifest.KubeletStatePath);

            // Write out certificate data.
            await File.WriteAllTextAsync(
                Path.Combine(manifest.KubeletStatePath, "ca.crt"),
                manifest.CaCertData,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(manifest.KubeletStatePath, "node.crt"),
                manifest.NodeCertData,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(manifest.KubeletStatePath, "node.key"),
                manifest.NodeKeyData,
                cancellationToken);

            // Write out kubeconfig file.
            await File.WriteAllTextAsync(
                Path.Combine(manifest.KubeletStatePath, "kubeconfig.yaml"),
                manifest.KubeConfigData,
                cancellationToken);

            // Write out the kubelet configuration file.
            string supplementalConfig;
            if (OperatingSystem.IsLinux())
            {
                supplementalConfig =
                    $$"""
                    # This option is Linux specific to deal with systemd's symlinking
                    # of /etc/resolv.conf.
                    resolvConf: "{{(Process.GetProcessesByName("systemd-resolved").Length > 0 ? "/run/systemd/resolve/resolv.conf" : "/etc/resolv.conf")}}"
                    """;
            }
            else
            {
                supplementalConfig =
                    $$"""
                    evictionHard: 
                      nodefs.available: "0%"
                      imagefs.available: "0%"
                    # These are Windows specific options that must be turned off
                    # because Windows doesn't support them.
                    resolvConf: ""
                    cgroupsPerQOS: false
                    enforceNodeAllocatable: []
                    # Required by Calico (but also on by default anyway).
                    enableDebuggingHandlers: true
                    # Required by Calico (but also the default value anyway).
                    hairpinMode: "promiscuous-bridge"
                    """;
            }
            await File.WriteAllTextAsync(
                Path.Combine(manifest.KubeletStatePath, "config.yaml"),
                $$"""
                kind: KubeletConfiguration
                apiVersion: kubelet.config.k8s.io/v1beta1
                authentication:
                  anonymous:
                    enabled: false
                  webhook:
                    enabled: true
                  x509:
                    clientCAFile: "{{Path.Combine(manifest.KubeletStatePath, "ca.crt").Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                authorization:
                  mode: Webhook
                staticPodURL: "http://127.0.0.1:8375/kubelet-static-pods"
                clusterDomain: "{{manifest.ClusterDomain}}"
                clusterDNS:
                  - "{{manifest.ClusterDns}}"
                runtimeRequestTimeout: "15m"
                tlsCertFile: "{{Path.Combine(manifest.KubeletStatePath, "node.crt").Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                tlsPrivateKeyFile: "{{Path.Combine(manifest.KubeletStatePath, "node.key").Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                registryNode: true
                {{supplementalConfig}}
                """,
                cancellationToken);

            // Create the kubelet process specification.
            var kubeletProcess = _processMonitorFactory.CreatePerpetualProcess(
                new PerpetualProcessSpecification(
                    filename: Path.Combine(kubeletInstallPath, $"kubelet{(OperatingSystem.IsWindows() ? ".exe" : "")}"),
                    arguments:
                    [
                        $"--config={Path.Combine(manifest.KubeletStatePath, "config.yaml")}",
                        $"--container-runtime-endpoint={manifest.ContainerdEndpoint}",
                        $"--kubeconfig={Path.Combine(manifest.KubeletStatePath, "kubeconfig.yaml")}",
                        $"--root-dir={Path.Combine(manifest.KubeletStatePath, "state")}",
                        $"--cert-dir={Path.Combine(manifest.KubeletStatePath, "state", "pki")}",
                        $"--v=2",
                    ],
                    afterStart: _ =>
                    {
                        _kubeletStarted.Open();
                        return Task.CompletedTask;
                    }));

            // Fix up the system environment for kubelet. This must be done here, and not part of the Helm chart,
            // as Kubelet will refuse to run at all without these preconditions met.
            if (OperatingSystem.IsLinux())
            {
                _logger.LogInformation("Turning off swap...");
                if (await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/sbin/swapoff",
                        Arguments = ["-a"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken) != 0)
                {
                    _logger.LogWarning("Turning off swap failed with non-zero exit code.");
                }

                _logger.LogInformation("Loading br_netfilter kernel module...");
                if (await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/sbin/modprobe",
                        Arguments = ["br_netfilter"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken) != 0)
                {
                    _logger.LogWarning("Loading br_netfilter kernel module failed with non-zero exit code.");
                }

                _logger.LogInformation("Enabling bridging for containers...");
                if (await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/sbin/sysctl",
                        Arguments = ["net.bridge.bridge-nf-call-iptables=1"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken) != 0)
                {
                    _logger.LogWarning("Enabling bridging for containers failed with non-zero exit code.");
                }
            }

            // Start kubelet.
            _logger.LogInformation($"Starting kubelet process...");
            try
            {
                await kubeletProcess.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            _logger.LogInformation($"kubelet has exited.");
        }

        private async Task RunAsync()
        {
            // Terminate any existing kubelet processes.
            await _processKiller.EnsureProcessesAreNotRunning([
                "kubelet",
                ], CancellationToken.None);

            // Start the manifest poll from the main RKM service.
            await _genericManifestClient.RegisterAndRunWithManifestAsync(
                new Uri("ws://127.0.0.1:8375/kubelet"),
                InvocationContext?.ParseResult.GetValueForOption(_options.ManifestPath),
                ManifestJsonSerializerContext.Default.KubeletManifest,
                async (manifest, cancellationToken) =>
                {
                    try
                    {
                        await RunWithManifestAsync(manifest, cancellationToken);
                    }
                    finally
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            // If we exit this function, and cancellation isn't requested, something has gone wrong
                            // and the whole service should terminate (instead of us idling while the kubelet
                            // process isn't running).
                            _logger.LogError("Manifest execution loop exited unexpectedly.");
                            _hostApplicationLifetime.StopApplication();
                        }
                    }
                },
                _hostApplicationLifetime.ApplicationStopping);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Wait until containerd process is running.
            _logger.LogInformation("Waiting to see the containerd process running...");
            while (!cancellationToken.IsCancellationRequested)
            {
                var processes = Process.GetProcessesByName("containerd");
                if (processes.Length > 0)
                {
                    // We're good to go.
                    break;
                }

                // We are still waiting on containerd to start.
                await Task.Delay(2000, cancellationToken);
                _logger.LogInformation("Waiting to see the containerd process running...");
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Start in the background.
            _backgroundTask = Task.Run(
                async () =>
                {
                    try
                    {
                        // Run the main loop.
                        await RunAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        // If we ever exit this function, and the application is not already stopping, request it to stop now.
                        if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                        {
                            _logger.LogError("Primary execution loop exited unexpectedly.");
                            _hostApplicationLifetime.StopApplication();
                        }
                    }
                },
                cancellationToken);

            // Wait until kubelet starts, or the service start is cancelled.
            await _kubeletStarted.WaitAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // If we have a background task, wait for it to complete.
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unexpected exception when stopping kubelet: {ex.Message}");
                }
            }
        }
    }
}
