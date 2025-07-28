namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.IO;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The asset preparation component downloads and extracts the assets required
    /// for Kubernetes to run for all of the platforms and configurations.
    /// 
    /// Once assets are ready, it sets the <see cref="WellKnownFlags.AssetsReady"/> flag.
    /// </summary>
    internal partial class AssetPreparationComponent : IComponent
    {
        private readonly IAssetManager _assetManager;
        private readonly IPathProvider _pathProvider;

        public AssetPreparationComponent(
            IAssetManager assetManager,
            IPathProvider pathProvider)
        {
            _assetManager = assetManager;
            _pathProvider = pathProvider;
        }

        [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        [SupportedOSPlatform("linux")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int chmod(string pathname, int mode);

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken stoppingToken)
        {
            // We need to wait for OS networking to be ready in case the network connection needs to be interrupted.
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            if (OperatingSystem.IsLinux())
            {
                // Ensure the assets are downloaded.
                await _assetManager.EnsureAsset("RKM:Downloads:Containerd:Linux", "containerd.tar.gz", stoppingToken);
                await _assetManager.EnsureAsset("RKM:Downloads:KubernetesNode:Linux", "kubernetes-node.tar.gz", stoppingToken);
                await _assetManager.EnsureAsset("RKM:Downloads:Runc:Linux", "runc", stoppingToken);
                await _assetManager.EnsureAsset("RKM:Downloads:CniPlugins:Linux", "cni-plugins.tar.gz", stoppingToken);
                if (context.Role == RoleType.Controller)
                {
                    await _assetManager.EnsureAsset("RKM:Downloads:KubernetesServer:Linux", "kubernetes-server.tar.gz", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:Etcd:Linux", "etcd.tar.gz", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:CalicoCtl:Linux", "calicoctl", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:Helm:Linux", "helm.tar.gz", stoppingToken);
                }

                // Extract the assets.
                await _assetManager.ExtractAsset("containerd.tar.gz", Path.Combine(_pathProvider.RKMRoot, "containerd"), stoppingToken);
                Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "containerd-state"));
                await _assetManager.ExtractAsset("kubernetes-node.tar.gz", Path.Combine(_pathProvider.RKMRoot, "kubernetes-node"), stoppingToken);
                await _assetManager.ExtractAsset("cni-plugins.tar.gz", Path.Combine(_pathProvider.RKMRoot, "cni-plugins"), stoppingToken);
                if (!File.Exists(Path.Combine(_pathProvider.RKMRoot, "runc", "runc")) ||
                    !File.Exists(Path.Combine(_pathProvider.RKMRoot, "runc", "runc.version")) ||
                    File.ReadAllText(Path.Combine(_pathProvider.RKMRoot, "runc", "runc.version")) != _pathProvider.RKMVersion)
                {
                    Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "runc"));
                    File.Copy(
                        Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion, "runc"),
                        Path.Combine(_pathProvider.RKMRoot, "runc", "runc"),
                        true);
                    chmod(Path.Combine(_pathProvider.RKMRoot, "runc", "runc"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);
                    File.WriteAllText(Path.Combine(_pathProvider.RKMRoot, "runc", "runc.version"), _pathProvider.RKMVersion);
                }

                // On the controller, extract the controller-specific assets.
                if (context.Role == RoleType.Controller)
                {
                    await _assetManager.ExtractAsset("kubernetes-server.tar.gz", Path.Combine(_pathProvider.RKMRoot, "kubernetes-server"), stoppingToken);
                    await _assetManager.ExtractAsset("etcd.tar.gz", Path.Combine(_pathProvider.RKMRoot, "etcd"), stoppingToken, "etcd-v3.5.7-linux-amd64");
                    await _assetManager.ExtractAsset("helm.tar.gz", Path.Combine(_pathProvider.RKMRoot, "helm-bin"), stoppingToken, "linux-amd64");
                    chmod(Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion, "calicoctl"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);
                    chmod(Path.Combine(_pathProvider.RKMRoot, "helm-bin", "helm"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);
                }

                // On the controller, set up easy kubectl and calicoctl wrappers that the user can use
                // to interact with the cluster.
                if (context.Role == RoleType.Controller)
                {
                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "kubectl"), @"
#!/bin/bash

RKM_ROOT=$(dirname ""$0"")
KUBECONFIG=""$RKM_ROOT/kubeconfigs/users/user-admin.kubeconfig"" ""$RKM_ROOT/kubernetes-server/kubernetes/server/bin/kubectl"" $*
exit $?
".Trim().Replace("\r\n", "\n", StringComparison.Ordinal), stoppingToken);
                    chmod(Path.Combine(_pathProvider.RKMRoot, "kubectl"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);

                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "calicoctl"), @"
#!/bin/bash

RKM_ROOT=$(dirname ""$0"")
KUBECONFIG=""$RKM_ROOT/kubeconfigs/users/user-admin.kubeconfig"" ""$RKM_ROOT/assets/calicoctl"" $*
exit $?
".Trim().Replace("\r\n", "\n", StringComparison.Ordinal), stoppingToken);
                    chmod(Path.Combine(_pathProvider.RKMRoot, "calicoctl"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);

                    if (Directory.Exists(Path.Combine(_pathProvider.RKMRoot, "helm")))
                    {
                        // Clean up old "helm" directory that is now called "helm-bin".
                        await DirectoryAsync.DeleteAsync(Path.Combine(_pathProvider.RKMRoot, "helm"), true);
                    }
                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "helm"), @"
#!/bin/bash

RKM_ROOT=$(dirname ""$0"")
KUBECONFIG=""$RKM_ROOT/kubeconfigs/users/user-admin.kubeconfig"" ""$RKM_ROOT/helm-bin/helm"" $*
exit $?
".Trim().Replace("\r\n", "\n", StringComparison.Ordinal), stoppingToken);
                    chmod(Path.Combine(_pathProvider.RKMRoot, "helm"), 0x100 | 0x80 | 0x40 | 0x20 | 0x8 | 0x4 | 0x1);
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                // Ensure the assets are downloaded.
                await _assetManager.EnsureAsset("RKM:Downloads:Containerd:Windows", "containerd.tar.gz", stoppingToken);
                await _assetManager.EnsureAsset("RKM:Downloads:KubernetesNode:Windows", "kubernetes-node.tar.gz", stoppingToken);
                await _assetManager.EnsureAsset("RKM:Downloads:ContainerdForWin11:Windows", "redpoint-containerd-for-win11.zip", stoppingToken);
                if (context.Role == RoleType.Controller)
                {
                    // Get the assets needed to run kubelet in WSL.
                    await _assetManager.EnsureAsset("RKM:Downloads:Containerd:Linux", "wsl-containerd.tar.gz", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:KubernetesNode:Linux", "wsl-kubernetes-node.tar.gz", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:Runc:Linux", "wsl-runc", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:CniPlugins:Linux", "wsl-cni-plugins.tar.gz", stoppingToken);
                    // Get the assets needed to run the Kubernetes API server and etcd in WSL.
                    await _assetManager.EnsureAsset("RKM:Downloads:KubernetesServer:Linux", "kubernetes-server.tar.gz", stoppingToken);
                    await _assetManager.EnsureAsset("RKM:Downloads:Etcd:Linux", "etcd.tar.gz", stoppingToken);
                    // Windows calicoctl client is broken and tries to enforce policy that only applies to Windows nodes
                    // even when connecting to the API server that is running on Linux, so we have to WSL this.
                    await _assetManager.EnsureAsset("RKM:Downloads:CalicoCtl:Linux", "calicoctl", stoppingToken);
                    // Windows containers can't hit the CoreDNS service over UDP when Windows is running the Linux bits
                    // inside WSL. Workaround this issue by having Windows run it's own copy of CoreDNS.
                    await _assetManager.EnsureAsset("RKM:Downloads:CoreDNS:Windows", "coredns.tar.gz", stoppingToken);
                }

                // Extract the assets.
                await _assetManager.ExtractAsset("containerd.tar.gz", Path.Combine(_pathProvider.RKMRoot, "containerd"), stoppingToken);
                Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "containerd-state"));
                await _assetManager.ExtractAsset("kubernetes-node.tar.gz", Path.Combine(_pathProvider.RKMRoot, "kubernetes-node"), stoppingToken);
                await _assetManager.ExtractAsset("redpoint-containerd-for-win11.zip", Path.Combine(_pathProvider.RKMRoot, "containerd-redpoint"), stoppingToken);

                // Overwrite the shipped containerd with the Redpoint patched version so that LTSC2022 images work on Windows 11.
                File.Copy(
                    Path.Combine(_pathProvider.RKMRoot, "containerd-redpoint", "containerd.exe"),
                    Path.Combine(_pathProvider.RKMRoot, "containerd", "bin", "containerd.exe"),
                    true);

                // On the controller, extract the controller-specific assets.
                if (context.Role == RoleType.Controller)
                {
                    // Extract the assets needed to run kubelet into their own little "wsl" subdirectory (since we will also
                    // be running a Windows kubelet). There's no need to chmod runc since it will already be executable due to
                    // the way files on the main Windows filesystem are exposed to WSL.
                    await _assetManager.ExtractAsset("wsl-containerd.tar.gz", Path.Combine(_pathProvider.RKMRoot, "wsl", "containerd"), stoppingToken);
                    await _assetManager.ExtractAsset("wsl-kubernetes-node.tar.gz", Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node"), stoppingToken);
                    await _assetManager.ExtractAsset("wsl-cni-plugins.tar.gz", Path.Combine(_pathProvider.RKMRoot, "wsl", "cni-plugins"), stoppingToken);
                    if (!File.Exists(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc", "runc")) ||
                        !File.Exists(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc", "runc.version")) ||
                        File.ReadAllText(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc", "runc.version")) != _pathProvider.RKMVersion)
                    {
                        Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc"));
                        File.Copy(
                            Path.Combine(_pathProvider.RKMRoot, "assets", _pathProvider.RKMVersion, "wsl-runc"),
                            Path.Combine(_pathProvider.RKMRoot, "wsl", "runc", "runc"),
                            true);
                        File.WriteAllText(Path.Combine(_pathProvider.RKMRoot, "wsl", "runc", "runc.version"), _pathProvider.RKMVersion);
                    }

                    // Extract the assets needed to run the Kubernetes API server and etcd in WSL.
                    await _assetManager.ExtractAsset("kubernetes-server.tar.gz", Path.Combine(_pathProvider.RKMRoot, "kubernetes-server"), stoppingToken);
                    await _assetManager.ExtractAsset("etcd.tar.gz", Path.Combine(_pathProvider.RKMRoot, "etcd"), stoppingToken, "etcd-v3.5.7-linux-amd64");

                    // Extract the CoreDNS component needed for Windows kubelet when in controller mode.
                    await _assetManager.ExtractAsset("coredns.tar.gz", Path.Combine(_pathProvider.RKMRoot, "coredns"), stoppingToken);
                }

                // On the controller, set up easy kubectl wrappers that the user can use to interact with
                // the cluster.
                if (context.Role == RoleType.Controller)
                {
                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "kubectl.cmd"), @"
@echo off
set KUBECONFIG=%~dp0\kubeconfigs\users\user-admin.kubeconfig
%~dp0\kubernetes-node\kubernetes\node\bin\kubectl.exe %*
".Trim().Replace("\n", "\r\n", StringComparison.Ordinal), stoppingToken);
                }

                // On the controller, set up the calicoctl wrapper which is required for provisioning
                // (and can't be omitted because there is no way to set environment variables for WSL,
                // and the calicoctl binary for Windows is buggy).
                if (context.Role == RoleType.Controller)
                {
                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "calicoctl"), @"
#!/bin/bash

RKM_ROOT=$(dirname ""$0"")
KUBECONFIG=""$RKM_ROOT/kubeconfigs/users/user-admin.kubeconfig"" ""$RKM_ROOT/assets/calicoctl"" $*
exit $?
".Trim().Replace("\r\n", "\n", StringComparison.Ordinal), stoppingToken);
                }
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Assets are now ready.
            context.SetFlag(WellKnownFlags.AssetsReady);
        }
    }
}
