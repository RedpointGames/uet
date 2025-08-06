namespace UET.Commands.Cluster
{
    using Docker.Registry.DotNet.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.IO;
    using Redpoint.KubernetesManager.Manifests;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Manifest;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.ProcessExecution;
    using Redpoint.ServiceControl;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ClusterRunContainerdCommand
    {
        internal sealed class Options
        {
            public Option<string> ManifestPath = new Option<string>("--manifest-path", "The path to the cached manifest file to use across restarts. This file will be read on startup, and written to whenever we receive a new manifest from the RKM service.");
        }

        public static Command CreateRunContainerdCommand()
        {
            var options = new Options();
            var command = new Command("run-containerd");
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterRunContainerdCommandInstance>(
                options,
                services =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm-containerd";
                        });
                    }
                    services.AddRkmServiceHelpers(false, "rkm-containerd");
                    services.AddHostedService<ContainerdHostedService>();
                });
            command.IsHidden = true;
            return command;
        }

        private sealed class ClusterRunContainerdCommandInstance : ICommandInstance
        {
            private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

            public ClusterRunContainerdCommandInstance(
                IHostedServiceFromExecutable hostedServiceFromExecutable)
            {
                _hostedServiceFromExecutable = hostedServiceFromExecutable;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                // Store the invocation context so that we can get the command line arguments inside the hosted service.
                ContainerdHostedService.InvocationContext = context;

                await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
                return 0;
            }
        }

        private class ContainerdHostedService : IHostedService
        {
            private readonly IHostApplicationLifetime _hostApplicationLifetime;
            private readonly IGenericManifestClient _genericManifestClient;
            private readonly IProcessMonitorFactory _processMonitorFactory;
            private readonly IProcessExecutor _processExecutor;
            private readonly IServiceControl _serviceControl;
            private readonly ILogger<ContainerdHostedService> _logger;
            private readonly IProcessKiller _processKiller;
            private readonly Options _options;
            private readonly IWindowsHcsService? _windowsHcsService;
            private readonly Gate _containerdStarted;

            private Task? _backgroundTask;

            public static InvocationContext? InvocationContext;

            public ContainerdHostedService(
                IHostApplicationLifetime hostApplicationLifetime,
                IGenericManifestClient genericManifestClient,
                IProcessMonitorFactory processMonitorFactory,
                IProcessExecutor processExecutor,
                IServiceControl serviceControl,
                ILogger<ContainerdHostedService> logger,
                IProcessKiller processKiller,
                Options options,
                IWindowsHcsService? windowsHcsService = null)
            {
                _hostApplicationLifetime = hostApplicationLifetime;
                _genericManifestClient = genericManifestClient;
                _processMonitorFactory = processMonitorFactory;
                _processExecutor = processExecutor;
                _serviceControl = serviceControl;
                _logger = logger;
                _processKiller = processKiller;
                _options = options;
                _windowsHcsService = windowsHcsService;
                _containerdStarted = new Gate();
            }

            private async Task<bool> ExtractTarGz(
                MemoryStream archive,
                string target,
                CancellationToken cancellationToken,
                string? trimLeading = null)
            {
                using var gzip = new GZipStream(archive, CompressionMode.Decompress);
                using var tar = new TarReader(gzip);

                var anyFailures = false;

                while (tar.GetNextEntry() is TarEntry entry)
                {
                    var entryName = entry.Name;
                    if (trimLeading != null && entryName.StartsWith(trimLeading, StringComparison.Ordinal))
                    {
                        entryName = entryName.Substring(trimLeading.Length + 1);
                        if (string.IsNullOrWhiteSpace(entryName))
                        {
                            continue;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, entryName))!);
                    if (!entryName.EndsWith('\\') && !entryName.EndsWith('/') && !string.IsNullOrWhiteSpace(entryName))
                    {
                        _logger.LogInformation($"Extracting: {entryName}");
                        await entry.ExtractToFileAsync(Path.Combine(target, entryName + ".tmp"), overwrite: true, cancellationToken);
                        try
                        {
                            File.Move(Path.Combine(target, entryName + ".tmp"), Path.Combine(target, entryName), true);
                        }
                        catch (UnauthorizedAccessException) when (File.Exists(Path.Combine(target, entryName)))
                        {
                            _logger.LogWarning($"Unable to overwrite existing file: {Path.Combine(target, entryName)}");
                            anyFailures = true;
                        }
                    }
                }

                return !anyFailures;
            }

            private async Task<string?> DownloadAndInstallContainerdIfNeeded(
                ContainerdManifest manifest,
                CancellationToken cancellationToken)
            {
                // Log the version of containerd that we're about to run.
                var versionWithSuffix = manifest.ContainerdVersion;
                if (!string.IsNullOrWhiteSpace(manifest.RuncVersion) &&
                    !OperatingSystem.IsWindows())
                {
                    versionWithSuffix += "-" + manifest.RuncVersion;
                }
                versionWithSuffix += "-" + manifest.CniPluginsVersion;
                versionWithSuffix += manifest.FlannelCniVersionSuffix;
                versionWithSuffix += "-" + manifest.FlannelVersion;
                if (manifest.UseRedpointContainerd)
                {
                    versionWithSuffix += "-redpoint";
                }
                _logger.LogInformation($"Received manifest; attempting to start containerd '{versionWithSuffix}'...");

                // Check if containerd is already installed.
                var containerdInstallPath = Path.Combine(
                    manifest.ContainerdInstallRootPath,
                    versionWithSuffix);
                if (!Directory.Exists(containerdInstallPath) ||
                    !File.Exists(Path.Combine(containerdInstallPath, ".rkm-flag")))
                {
                    _logger.LogInformation($"Downloading and installing containerd '{versionWithSuffix}'...");

                    // Erase any previous partial download/extract.
                    if (Directory.Exists(containerdInstallPath))
                    {
                        await DirectoryAsync.DeleteAsync(containerdInstallPath, true);
                    }
                    Directory.CreateDirectory(containerdInstallPath);

                    // Download and extract containerd in-memory, without caching the download on disk.
                    {
                        var platformName = OperatingSystem.IsWindows() ? "windows" : "linux";
                        var containerdUri = new Uri($"https://github.com/containerd/containerd/releases/download/v{manifest.ContainerdVersion}/containerd-{manifest.ContainerdVersion}-{platformName}-amd64.tar.gz");
                        _logger.LogInformation($"Downloading and extracting primary containerd archive from '{containerdUri}'...");
                        using (var archiveMemory = new MemoryStream())
                        {
                            // Download the archive.
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(containerdUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(archiveMemory, cancellationToken);
                                }
                            }

                            // Rewind to the beginning.
                            archiveMemory.Seek(0, SeekOrigin.Begin);

                            // Extract containerd to the target directory.
                            if (!await ExtractTarGz(
                                archiveMemory,
                                containerdInstallPath,
                                cancellationToken))
                            {
                                _logger.LogError("Failed to extract containerd to installation path.");
                                return null;
                            }
                        }
                        _logger.LogInformation($"Downloaded and extracted primary containerd archive from '{containerdUri}' to '{containerdInstallPath}'.");
                    }

                    // If we are on Linux, we need to download runc.
                    if (OperatingSystem.IsLinux())
                    {
                        var runcUri = new Uri($"https://github.com/opencontainers/runc/releases/download/v{manifest.RuncVersion}/runc.amd64");
                        var runcPath = Path.Combine(containerdInstallPath, "bin", "runc");
                        _logger.LogInformation($"Downloading runc from '{runcUri}'...");
                        using (var fileStream = new FileStream(runcPath + ".tmp", FileMode.Create, FileAccess.ReadWrite))
                        {
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(runcUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(fileStream, cancellationToken);
                                }
                            }
                        }
                        File.SetUnixFileMode(
                            runcPath + ".tmp",
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        File.Move(runcPath + ".tmp", runcPath, true);
                        _logger.LogInformation($"Downloaded runc from '{runcUri}' to '{runcPath}'.");
                    }

                    // If we are on Windows, we may need to download the Redpoint variant of containerd.
                    if (OperatingSystem.IsWindows() && manifest.UseRedpointContainerd)
                    {
                        var redpointUri = new Uri($"https://dl-public.redpoint.games/file/dl-public-redpoint-games/redpoint-containerd-for-win11-{manifest.ContainerdVersion}.zip");
                        _logger.LogInformation($"Downloading and extracting Redpoint containerd archive from '{redpointUri}'...");
                        using (var archiveMemory = new MemoryStream())
                        {
                            // Download the archive.
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(redpointUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(archiveMemory, cancellationToken);
                                }
                            }

                            // Rewind to the beginning.
                            archiveMemory.Seek(0, SeekOrigin.Begin);

                            // Extract containerd over the top of the existing containerd binary.
                            var foundContainerdOverride = false;
                            using var zip = new ZipArchive(archiveMemory, ZipArchiveMode.Read, true);
                            foreach (var entry in zip.Entries)
                            {
                                if (entry.Name == "containerd.exe")
                                {
                                    var targetPath = Path.Combine(
                                        containerdInstallPath,
                                        "bin",
                                        "containerd.exe");
                                    _logger.LogInformation($"Extracting '{entry.FullName}' to '{targetPath}'...");
                                    entry.ExtractToFile(
                                        targetPath,
                                        true);
                                    foundContainerdOverride = true;
                                }
                            }
                            if (!foundContainerdOverride)
                            {
                                _logger.LogError("Unable to find containerd.exe within Redpoint containerd archive.");
                                return null;
                            }
                        }
                        _logger.LogInformation($"Downloaded and extracted Redpoint containerd archive from '{redpointUri}'.");
                    }

                    // Determine location to install CNI plugins.
                    var cniPluginsDirectory = Path.Combine(containerdInstallPath, "cni-plugins");
                    Directory.CreateDirectory(cniPluginsDirectory);

                    // Download and extract the CNI plugins.
                    {
                        var platformName = OperatingSystem.IsWindows() ? "windows" : "linux";
                        var cniPluginsUri = new Uri($"https://github.com/containernetworking/plugins/releases/download/v{manifest.CniPluginsVersion}/cni-plugins-{platformName}-amd64-v{manifest.CniPluginsVersion}.tgz");
                        _logger.LogInformation($"Downloading and extracting CNI plugins archive from '{cniPluginsUri}'...");
                        using (var archiveMemory = new MemoryStream())
                        {
                            // Download the archive.
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(cniPluginsUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(archiveMemory, cancellationToken);
                                }
                            }

                            // Rewind to the beginning.
                            archiveMemory.Seek(0, SeekOrigin.Begin);

                            // Extract CNI plugins to the target directory.
                            if (!await ExtractTarGz(
                                archiveMemory,
                                cniPluginsDirectory,
                                cancellationToken))
                            {
                                _logger.LogError("Failed to extract CNI plugins to installation path.");
                                return null;
                            }
                        }
                        _logger.LogInformation($"Downloaded and extracted CNI plugins archive from '{cniPluginsUri}' to '{cniPluginsDirectory}'.");
                    }

                    // Download and extract the flannel CNI plugin.
                    {
                        var platformName = OperatingSystem.IsWindows() ? "windows" : "linux";
                        var flannelPluginUri = new Uri($"https://github.com/flannel-io/cni-plugin/releases/download/v{manifest.CniPluginsVersion}{manifest.FlannelCniVersionSuffix}/cni-plugin-flannel-{platformName}-amd64-v{manifest.CniPluginsVersion}{manifest.FlannelCniVersionSuffix}.tgz");
                        _logger.LogInformation($"Downloading and extracting flannel CNI plugin archive from '{flannelPluginUri}'...");
                        using (var archiveMemory = new MemoryStream())
                        {
                            // Download the archive.
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(flannelPluginUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(archiveMemory, cancellationToken);
                                }
                            }

                            // Rewind to the beginning.
                            archiveMemory.Seek(0, SeekOrigin.Begin);

                            // Extract CNI plugins to the target directory.
                            if (!await ExtractTarGz(
                                archiveMemory,
                                cniPluginsDirectory,
                                cancellationToken))
                            {
                                _logger.LogError("Failed to extract flannel CNI plugin to installation path.");
                                return null;
                            }
                        }
                        var archFile = Path.Combine(cniPluginsDirectory, "flannel-amd64" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
                        var nonArchFile = Path.Combine(cniPluginsDirectory, "flannel" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
                        if (File.Exists(archFile))
                        {
                            File.Move(archFile, nonArchFile, true);
                        }
                        _logger.LogInformation($"Downloaded and extracted flannel CNI plugin archive from '{flannelPluginUri}' to '{cniPluginsDirectory}'.");
                    }

                    // Download and extract the flannel daemon. We just put it in the CNI plugins directory
                    // like RKE2 does.
                    {
                        var platformName = OperatingSystem.IsWindows() ? "windows" : "linux";
                        var flannelPluginUri = new Uri($"https://github.com/flannel-io/flannel/releases/download/v{manifest.FlannelVersion}/flannel-v{manifest.FlannelVersion}-{platformName}-amd64.tar.gz");
                        _logger.LogInformation($"Downloading and extracting flannel daemon archive from '{flannelPluginUri}'...");
                        using (var archiveMemory = new MemoryStream())
                        {
                            // Download the archive.
                            using (var httpClient = new HttpClient())
                            {
                                using (var stream = await httpClient.GetStreamAsync(flannelPluginUri, cancellationToken))
                                {
                                    await stream.CopyToAsync(archiveMemory, cancellationToken);
                                }
                            }

                            // Rewind to the beginning.
                            archiveMemory.Seek(0, SeekOrigin.Begin);

                            // Extract CNI plugins to the target directory.
                            if (!await ExtractTarGz(
                                archiveMemory,
                                cniPluginsDirectory,
                                cancellationToken))
                            {
                                _logger.LogError("Failed to extract flannel daemon to installation path.");
                                return null;
                            }
                        }
                        _logger.LogInformation($"Downloaded and extracted flannel daemon archive from '{flannelPluginUri}' to '{cniPluginsDirectory}'.");
                    }

                    // Mark this install as having been completed.
                    File.WriteAllText(
                        Path.Combine(containerdInstallPath, ".rkm-flag"),
                        "ok");
                }

                // Log that containerd is now ready on disk.
                _logger.LogInformation($"Containerd '{versionWithSuffix}' is now ready on disk.");
                return containerdInstallPath;
            }

            private async Task RunWithManifestAsync(ContainerdManifest manifest, CancellationToken cancellationToken)
            {
                // Download and install containerd.
                var containerdInstallPath = await DownloadAndInstallContainerdIfNeeded(
                    manifest,
                    cancellationToken);
                if (containerdInstallPath == null)
                {
                    return;
                }

                // Write out the containerd configuration file.
                Directory.CreateDirectory(manifest.ContainerdStatePath);
                {
                    var rootDir = Path.Combine(manifest.ContainerdStatePath, "root")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var stateDir = Path.Combine(manifest.ContainerdStatePath, "state")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var endpointAddress = manifest.ContainerdEndpointPath
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var cniBinDir = Path.Combine(containerdInstallPath, "cni-plugins")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var cniConfDir = Path.Combine(manifest.ContainerdStatePath, "cni", "net.d")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var optDir = Path.Combine(manifest.ContainerdStatePath, "root", "opt")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var ocicryptKeysDir = Path.Combine(manifest.ContainerdStatePath, "ocicrypt", "keys")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    var ocicryptKeyProvider = Path.Combine(manifest.ContainerdStatePath, "ocicrypt", "ocicrypt_keyprovider.conf")
                        .Replace("\\", "\\\\", StringComparison.Ordinal);
                    if (OperatingSystem.IsLinux())
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(manifest.ContainerdStatePath, "config.yaml"),
                            $$"""
                            disabled_plugins = []
                            imports = []
                            oom_score = 0
                            plugin_dir = ""
                            required_plugins = []
                            root = "{{rootDir}}"
                            state = "{{stateDir}}"
                            temp = ""
                            version = 2

                            [cgroup]
                              path = ""

                            [debug]
                              address = ""
                              format = ""
                              gid = 0
                              level = ""
                              uid = 0

                            [grpc]
                              address = "{{endpointAddress}}"
                              gid = 0
                              max_recv_message_size = 16777216
                              max_send_message_size = 16777216
                              tcp_address = ""
                              tcp_tls_ca = ""
                              tcp_tls_cert = ""
                              tcp_tls_key = ""
                              uid = 0

                            [metrics]
                              address = ""
                              grpc_histogram = false

                            [plugins]

                              [plugins."io.containerd.gc.v1.scheduler"]
                                deletion_threshold = 0
                                mutation_threshold = 100
                                pause_threshold = 0.02
                                schedule_delay = "0s"
                                startup_delay = "100ms"

                              [plugins."io.containerd.grpc.v1.cri"]
                                device_ownership_from_security_context = false
                                disable_apparmor = false
                                disable_cgroup = false
                                disable_hugetlb_controller = true
                                disable_proc_mount = false
                                disable_tcp_service = true
                                enable_selinux = false
                                enable_tls_streaming = false
                                enable_unprivileged_icmp = false
                                enable_unprivileged_ports = false
                                ignore_image_defined_volumes = false
                                max_concurrent_downloads = 3
                                max_container_log_line_size = 16384
                                netns_mounts_under_state_dir = false
                                restrict_oom_score_adj = false
                                sandbox_image = "registry.k8s.io/pause:3.6"
                                selinux_category_range = 1024
                                stats_collect_period = 10
                                stream_idle_timeout = "4h0m0s"
                                stream_server_address = "127.0.0.1"
                                stream_server_port = "0"
                                systemd_cgroup = false
                                tolerate_missing_hugetlb_controller = true
                                unset_seccomp_profile = ""

                                [plugins."io.containerd.grpc.v1.cri".cni]
                                  bin_dir = "{{cniBinDir}}"
                                  conf_dir = "{{cniConfDir}}"
                                  conf_template = ""
                                  ip_pref = ""
                                  max_conf_num = 1

                                [plugins."io.containerd.grpc.v1.cri".containerd]
                                  default_runtime_name = "runc"
                                  disable_snapshot_annotations = true
                                  discard_unpacked_layers = false
                                  ignore_rdt_not_enabled_errors = false
                                  no_pivot = false
                                  snapshotter = "overlayfs"

                                  [plugins."io.containerd.grpc.v1.cri".containerd.default_runtime]
                                    base_runtime_spec = ""
                                    cni_conf_dir = ""
                                    cni_max_conf_num = 0
                                    container_annotations = []
                                    pod_annotations = []
                                    privileged_without_host_devices = false
                                    runtime_engine = ""
                                    runtime_path = ""
                                    runtime_root = ""
                                    runtime_type = ""

                                    [plugins."io.containerd.grpc.v1.cri".containerd.default_runtime.options]

                                  [plugins."io.containerd.grpc.v1.cri".containerd.runtimes]

                                    [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runc]
                                      base_runtime_spec = ""
                                      cni_conf_dir = ""
                                      cni_max_conf_num = 0
                                      container_annotations = []
                                      pod_annotations = []
                                      privileged_without_host_devices = false
                                      runtime_engine = ""
                                      runtime_path = ""
                                      runtime_root = ""
                                      runtime_type = "io.containerd.runc.v2"

                                      [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runc.options]
                                        BinaryName = "{{containerdInstallPath}}/bin/runc"
                                        CriuImagePath = ""
                                        CriuPath = ""
                                        CriuWorkPath = ""
                                        IoGid = 0
                                        IoUid = 0
                                        NoNewKeyring = false
                                        NoPivotRoot = false
                                        Root = ""
                                        ShimCgroup = ""
                                        SystemdCgroup = true

                                  [plugins."io.containerd.grpc.v1.cri".containerd.untrusted_workload_runtime]
                                    base_runtime_spec = ""
                                    cni_conf_dir = ""
                                    cni_max_conf_num = 0
                                    container_annotations = []
                                    pod_annotations = []
                                    privileged_without_host_devices = false
                                    runtime_engine = ""
                                    runtime_path = ""
                                    runtime_root = ""
                                    runtime_type = ""

                                    [plugins."io.containerd.grpc.v1.cri".containerd.untrusted_workload_runtime.options]

                                [plugins."io.containerd.grpc.v1.cri".image_decryption]
                                  key_model = "node"

                                [plugins."io.containerd.grpc.v1.cri".registry]
                                  config_path = ""

                                  [plugins."io.containerd.grpc.v1.cri".registry.auths]

                                  [plugins."io.containerd.grpc.v1.cri".registry.configs]

                                  [plugins."io.containerd.grpc.v1.cri".registry.headers]

                                  [plugins."io.containerd.grpc.v1.cri".registry.mirrors]

                                [plugins."io.containerd.grpc.v1.cri".x509_key_pair_streaming]
                                  tls_cert_file = ""
                                  tls_key_file = ""

                              [plugins."io.containerd.internal.v1.opt"]
                                path = "{{optDir}}"

                              [plugins."io.containerd.internal.v1.restart"]
                                interval = "10s"

                              [plugins."io.containerd.internal.v1.tracing"]
                                sampling_ratio = 1.0
                                service_name = "containerd"

                              [plugins."io.containerd.metadata.v1.bolt"]
                                content_sharing_policy = "shared"

                              [plugins."io.containerd.monitor.v1.cgroups"]
                                no_prometheus = false

                              [plugins."io.containerd.runtime.v1.linux"]
                                no_shim = false
                                runtime = "runc"
                                runtime_root = ""
                                shim = "containerd-shim"
                                shim_debug = false

                              [plugins."io.containerd.runtime.v2.task"]
                                platforms = ["linux/amd64"]
                                sched_core = false

                              [plugins."io.containerd.service.v1.diff-service"]
                                default = ["walking"]

                              [plugins."io.containerd.service.v1.tasks-service"]
                                rdt_config_file = ""

                              [plugins."io.containerd.snapshotter.v1.aufs"]
                                root_path = ""

                              [plugins."io.containerd.snapshotter.v1.btrfs"]
                                root_path = ""

                              [plugins."io.containerd.snapshotter.v1.devmapper"]
                                async_remove = false
                                base_image_size = ""
                                discard_blocks = false
                                fs_options = ""
                                fs_type = ""
                                pool_name = ""
                                root_path = ""

                              [plugins."io.containerd.snapshotter.v1.native"]
                                root_path = ""

                              [plugins."io.containerd.snapshotter.v1.overlayfs"]
                                root_path = ""
                                upperdir_label = false

                              [plugins."io.containerd.snapshotter.v1.zfs"]
                                root_path = ""

                              [plugins."io.containerd.tracing.processor.v1.otlp"]
                                endpoint = ""
                                insecure = false
                                protocol = ""

                            [proxy_plugins]

                            [stream_processors]

                              [stream_processors."io.containerd.ocicrypt.decoder.v1.tar"]
                                accepts = ["application/vnd.oci.image.layer.v1.tar+encrypted"]
                                args = ["--decryption-keys-path", "{{ocicryptKeysDir}}"]
                                env = ["OCICRYPT_KEYPROVIDER_CONFIG={{ocicryptKeyProvider}}"]
                                path = "ctd-decoder"
                                returns = "application/vnd.oci.image.layer.v1.tar"

                              [stream_processors."io.containerd.ocicrypt.decoder.v1.tar.gzip"]
                                accepts = ["application/vnd.oci.image.layer.v1.tar+gzip+encrypted"]
                                args = ["--decryption-keys-path", "{{ocicryptKeysDir}}"]
                                env = ["OCICRYPT_KEYPROVIDER_CONFIG={{ocicryptKeyProvider}}"]
                                path = "ctd-decoder"
                                returns = "application/vnd.oci.image.layer.v1.tar+gzip"

                            [timeouts]
                              "io.containerd.timeout.bolt.open" = "0s"
                              "io.containerd.timeout.shim.cleanup" = "5s"
                              "io.containerd.timeout.shim.load" = "5s"
                              "io.containerd.timeout.shim.shutdown" = "3s"
                              "io.containerd.timeout.task.state" = "2s"

                            [ttrpc]
                              address = ""
                              gid = 0
                              uid = 0
                            """,
                            cancellationToken);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(manifest.ContainerdStatePath, "config.yaml"),
                            $$"""
                            disabled_plugins = []
                            imports = []
                            oom_score = 0
                            plugin_dir = ""
                            required_plugins = []
                            root = "{{rootDir}}"
                            state = "{{stateDir}}"
                            temp = ""
                            version = 2

                            [cgroup]
                              path = ""

                            [debug]
                              address = ""
                              format = ""
                              gid = 0
                              level = ""
                              uid = 0

                            [grpc]
                              address = "{{endpointAddress}}"
                              gid = 0
                              max_recv_message_size = 16777216
                              max_send_message_size = 16777216
                              tcp_address = ""
                              tcp_tls_ca = ""
                              tcp_tls_cert = ""
                              tcp_tls_key = ""
                              uid = 0

                            [metrics]
                              address = ""
                              grpc_histogram = false

                            [plugins]

                              [plugins."io.containerd.gc.v1.scheduler"]
                                deletion_threshold = 0
                                mutation_threshold = 100
                                pause_threshold = 0.02
                                schedule_delay = "0s"
                                startup_delay = "100ms"

                              [plugins."io.containerd.grpc.v1.cri"]
                                device_ownership_from_security_context = false
                                disable_apparmor = false
                                disable_cgroup = false
                                disable_hugetlb_controller = false
                                disable_proc_mount = false
                                disable_tcp_service = true
                                enable_selinux = false
                                enable_tls_streaming = false
                                enable_unprivileged_icmp = false
                                enable_unprivileged_ports = false
                                ignore_image_defined_volumes = false
                                max_concurrent_downloads = 3
                                max_container_log_line_size = 16384
                                netns_mounts_under_state_dir = false
                                restrict_oom_score_adj = false
                                sandbox_image = "registry.k8s.io/pause:3.6"
                                selinux_category_range = 0
                                stats_collect_period = 10
                                stream_idle_timeout = "4h0m0s"
                                stream_server_address = "127.0.0.1"
                                stream_server_port = "0"
                                systemd_cgroup = false
                                tolerate_missing_hugetlb_controller = false
                                unset_seccomp_profile = ""

                                [plugins."io.containerd.grpc.v1.cri".cni]
                                  bin_dir = "{{cniBinDir}}"
                                  conf_dir = "{{cniConfDir}}"
                                  conf_template = ""
                                  ip_pref = ""
                                  max_conf_num = 1

                                [plugins."io.containerd.grpc.v1.cri".containerd]
                                  default_runtime_name = "runhcs-wcow-process"
                                  disable_snapshot_annotations = false
                                  discard_unpacked_layers = false
                                  ignore_rdt_not_enabled_errors = false
                                  no_pivot = false
                                  snapshotter = "windows"

                                  [plugins."io.containerd.grpc.v1.cri".containerd.default_runtime]
                                    base_runtime_spec = ""
                                    cni_conf_dir = ""
                                    cni_max_conf_num = 0
                                    container_annotations = []
                                    pod_annotations = []
                                    privileged_without_host_devices = false
                                    runtime_engine = ""
                                    runtime_path = ""
                                    runtime_root = ""
                                    runtime_type = ""

                                    [plugins."io.containerd.grpc.v1.cri".containerd.default_runtime.options]

                                  [plugins."io.containerd.grpc.v1.cri".containerd.runtimes]

                                    [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runhcs-wcow-process]
                                      base_runtime_spec = ""
                                      cni_conf_dir = ""
                                      cni_max_conf_num = 0
                                      container_annotations = []
                                      pod_annotations = []
                                      privileged_without_host_devices = false
                                      runtime_engine = ""
                                      runtime_path = ""
                                      runtime_root = ""
                                      runtime_type = "io.containerd.runhcs.v1"

                                      [plugins."io.containerd.grpc.v1.cri".containerd.runtimes.runhcs-wcow-process.options]

                                  [plugins."io.containerd.grpc.v1.cri".containerd.untrusted_workload_runtime]
                                    base_runtime_spec = ""
                                    cni_conf_dir = ""
                                    cni_max_conf_num = 0
                                    container_annotations = []
                                    pod_annotations = []
                                    privileged_without_host_devices = false
                                    runtime_engine = ""
                                    runtime_path = ""
                                    runtime_root = ""
                                    runtime_type = ""

                                    [plugins."io.containerd.grpc.v1.cri".containerd.untrusted_workload_runtime.options]

                                [plugins."io.containerd.grpc.v1.cri".image_decryption]
                                  key_model = "node"

                                [plugins."io.containerd.grpc.v1.cri".registry]
                                  config_path = ""

                                  [plugins."io.containerd.grpc.v1.cri".registry.auths]

                                  [plugins."io.containerd.grpc.v1.cri".registry.configs]

                                  [plugins."io.containerd.grpc.v1.cri".registry.headers]

                                  [plugins."io.containerd.grpc.v1.cri".registry.mirrors]

                                [plugins."io.containerd.grpc.v1.cri".x509_key_pair_streaming]
                                  tls_cert_file = ""
                                  tls_key_file = ""

                              [plugins."io.containerd.internal.v1.opt"]
                                path = "{{optDir}}"

                              [plugins."io.containerd.internal.v1.restart"]
                                interval = "10s"

                              [plugins."io.containerd.internal.v1.tracing"]
                                sampling_ratio = 1.0
                                service_name = "containerd"

                              [plugins."io.containerd.metadata.v1.bolt"]
                                content_sharing_policy = "shared"

                              [plugins."io.containerd.runtime.v2.task"]
                                platforms = ["windows/amd64", "linux/amd64"]
                                sched_core = false

                              [plugins."io.containerd.service.v1.diff-service"]
                                default = ["windows", "windows-lcow"]

                              [plugins."io.containerd.service.v1.tasks-service"]
                                rdt_config_file = ""

                              [plugins."io.containerd.tracing.processor.v1.otlp"]
                                endpoint = ""
                                insecure = false
                                protocol = ""

                            [proxy_plugins]

                            [stream_processors]

                              [stream_processors."io.containerd.ocicrypt.decoder.v1.tar"]
                                accepts = ["application/vnd.oci.image.layer.v1.tar+encrypted"]
                                args = ["--decryption-keys-path", "{{ocicryptKeysDir}}"]
                                env = ["OCICRYPT_KEYPROVIDER_CONFIG={{ocicryptKeyProvider}}"]
                                path = "ctd-decoder"
                                returns = "application/vnd.oci.image.layer.v1.tar"

                              [stream_processors."io.containerd.ocicrypt.decoder.v1.tar.gzip"]
                                accepts = ["application/vnd.oci.image.layer.v1.tar+gzip+encrypted"]
                                args = ["--decryption-keys-path", "{{ocicryptKeysDir}}"]
                                env = ["OCICRYPT_KEYPROVIDER_CONFIG={{ocicryptKeyProvider}}"]
                                path = "ctd-decoder"
                                returns = "application/vnd.oci.image.layer.v1.tar+gzip"

                            [timeouts]
                              "io.containerd.timeout.bolt.open" = "0s"
                              "io.containerd.timeout.shim.cleanup" = "5s"
                              "io.containerd.timeout.shim.load" = "5s"
                              "io.containerd.timeout.shim.shutdown" = "3s"
                              "io.containerd.timeout.task.state" = "2s"

                            [ttrpc]
                              address = ""
                              gid = 0
                              uid = 0
                            """,
                            cancellationToken);
                    }
                }

                // Create a symlink so that flanneld can be launched inside containers.
                if (!string.IsNullOrWhiteSpace(manifest.CniPluginsSymlinkPath))
                {
                    var isValidLink = false;
                    var symbolicLinkTarget = Path.GetRelativePath(
                        Path.GetDirectoryName(manifest.CniPluginsSymlinkPath)!,
                        Path.Combine(containerdInstallPath, "cni-plugins"));
                    {
                        FileInfo pathInfo = new FileInfo(manifest.CniPluginsSymlinkPath);
                        if (pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            if (pathInfo.LinkTarget == symbolicLinkTarget)
                            {
                                _logger.LogInformation($"Existing symbolic link is already valid at '{manifest.CniPluginsSymlinkPath}'.");
                                isValidLink = true;
                            }
                            else
                            {
                                _logger.LogInformation($"Deleting existing symbolic link at '{manifest.CniPluginsSymlinkPath}'...");
                                File.Delete(manifest.CniPluginsSymlinkPath);
                            }
                        }
                    }
                    if (!isValidLink)
                    {
                        if (File.Exists(manifest.CniPluginsSymlinkPath))
                        {
                            _logger.LogInformation($"Deleting existing file at '{manifest.CniPluginsSymlinkPath}'...");
                            File.Delete(manifest.CniPluginsSymlinkPath);
                        }
                        if (Directory.Exists(manifest.CniPluginsSymlinkPath))
                        {
                            _logger.LogInformation($"Deleting existing directory at '{manifest.CniPluginsSymlinkPath}'...");
                            await DirectoryAsync.DeleteAsync(manifest.CniPluginsSymlinkPath, true);
                        }
                        _logger.LogInformation($"Creating symbolic link at '{manifest.CniPluginsSymlinkPath}' with target '{symbolicLinkTarget}'...");
                        Directory.CreateSymbolicLink(
                            manifest.CniPluginsSymlinkPath,
                            symbolicLinkTarget);
                    }
                }

                // Create the containerd process specification.
                var containerdProcess = _processMonitorFactory.CreatePerpetualProcess(
                    new Redpoint.KubernetesManager.Models.ProcessSpecification(
                        filename: Path.Combine(containerdInstallPath, "bin", "containerd"),
                        arguments:
                        [
                            "--config",
                            Path.Combine(manifest.ContainerdStatePath, "config.yaml")
                        ],
                        afterStart: _ =>
                        {
                            _containerdStarted.Open();
                            return Task.CompletedTask;
                        }));

                // Create a cancellation token that we can use to stop containerd.
                using var terminateContainerd = new CancellationTokenSource();

                // Start containerd and use our custom cancellation token.
                _logger.LogInformation($"Starting containerd process...");
                var containerdTask = containerdProcess.RunAsync(terminateContainerd.Token);

                // Wait until the main cancellation token is cancelled.
                try
                {
                    _logger.LogInformation($"Waiting for termination signal...");
                    await Task.Delay(-1, cancellationToken);
                }
                catch
                {
                }
                _logger.LogInformation($"containerd has been asked to shutdown.");

                // We've been asked to stop containerd. If the host is fully shutting down,
                // clean up the containers before that happens.
                var kubeletServiceName = OperatingSystem.IsWindows() ? "RKM - Kubelet" : "rkm-kubelet";
                var kubeletStopped = false;
                if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    // Stop the kubelet service, if it exists. This will prevent kubelet from
                    // scheduling containers while we're cleaning up.
                    try
                    {
                        if (await _serviceControl.IsServiceRunning(kubeletServiceName))
                        {
                            _logger.LogInformation($"Stopping the kubelet service...");
                            await _serviceControl.StopService(kubeletServiceName);
                            kubeletStopped = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Unable to stop kubelet service: {ex.Message}");
                    }

                    // Clean up the containers.
                    try
                    {
                        _logger.LogInformation($"Fetching a list of containers to stop...");
                        var containerListStringBuilder = new StringBuilder();
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = Path.Combine(containerdInstallPath, "bin", "ctr"),
                                Arguments =
                                [
                                    "--namespace",
                                    "k8s.io",
                                    "c",
                                    "list"
                                ]
                            },
                            CaptureSpecification.CreateFromStdoutStringBuilder(containerListStringBuilder),
                            _hostApplicationLifetime.ApplicationStopped);
                        var listLines = containerListStringBuilder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
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
                            await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = Path.Combine(containerdInstallPath, "bin", "ctr"),
                                    Arguments =
                                    [
                                        "--namespace",
                                        "k8s.io",
                                        "c",
                                        "delete",
                                        containerId,
                                    ]
                                },
                                CaptureSpecification.Passthrough,
                                _hostApplicationLifetime.ApplicationStopped);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to clean up containers: {ex.Message}");
                    }
                }

                // Terminate the containerd process.
                _logger.LogInformation($"Terminating the containerd process...");
                terminateContainerd.Cancel();

                // Wait for containerd to stop.
                try
                {
                    await containerdTask;
                }
                catch
                {
                }
                terminateContainerd.Dispose();
                _logger.LogInformation($"containerd has exited.");

                // Unmount and remove any remaining containers.
                if (OperatingSystem.IsLinux())
                {
                    // Unmount anything under the RKM root that appears in /etc/mtab, so that it's safe
                    // to delete an RKM install without running into "resource busy".
                    var mounts = await File.ReadAllLinesAsync("/etc/mtab", cancellationToken);
                    foreach (var mount in mounts)
                    {
                        var mountComponents = mount.Split(' ');
                        if (mountComponents.Length > 2 && mountComponents[1].StartsWith(manifest.ContainerdStatePath, StringComparison.Ordinal))
                        {
                            _logger.LogInformation($"Unmounting container folder due to shutdown: {mountComponents[1]}");

                            var unmountExitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = "/usr/bin/umount",
                                    Arguments = [mountComponents[1]],
                                },
                                CaptureSpecification.Passthrough,
                                CancellationToken.None);
                            if (unmountExitCode != 0)
                            {
                                _logger.LogWarning($"Unmount operation failed for: {mountComponents[1]}");
                            }
                        }
                    }

                    _logger.LogInformation($"Unmounted all container folders.");
                }
                else if (OperatingSystem.IsWindows() && _windowsHcsService != null)
                {
                    foreach (var computeSystem in _windowsHcsService.GetHcsComputeSystems())
                    {
                        if (computeSystem.SystemType == "Container")
                        {
                            _logger.LogInformation($"Killing HCS compute system {computeSystem.Id}...");
                            _windowsHcsService.TerminateHcsSystem(computeSystem.Id);
                        }
                    }
                }

                // If we stopped the kubelet service, we now have to start the kubelet service again so
                // that the running state of the service is preserved across containerd restarts.
                if (kubeletStopped)
                {
                    try
                    {
                        _logger.LogInformation($"Starting the kubelet service to restore it's state...");
                        await _serviceControl.StartService(kubeletServiceName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Unable to start kubelet service: {ex.Message}");
                    }
                }
            }

            private async Task RunAsync()
            {
                // Terminate any existing containerd processes.
                await _processKiller.EnsureProcessesAreNotRunning([
                    "containerd",
                    "containerd-shim-runhcs-v1",
                ], CancellationToken.None);

                // Start the manifest poll from the main RKM service.
                await _genericManifestClient.RegisterAndRunWithManifestAsync(
                    new Uri("ws://127.0.0.1:8375/containerd"),
                    InvocationContext?.ParseResult.GetValueForOption(_options.ManifestPath),
                    ManifestJsonSerializerContext.Default.ContainerdManifest,
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
                                // and the whole service should terminate (instead of us idling while the containerd
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

                // Wait until containerd starts, or the service start is cancelled.
                await _containerdStarted.WaitAsync(cancellationToken);
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
                        _logger.LogError(ex, $"Unexpected exception when stopping containerd: {ex.Message}");
                    }
                }
            }
        }
    }
}
