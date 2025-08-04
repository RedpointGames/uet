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
                await _assetManager.EnsureAsset("RKM:Downloads:CniPlugins:Linux", "cni-plugins.tar.gz", stoppingToken);
                if (context.Role == RoleType.Controller)
                {
                    await _assetManager.EnsureAsset("RKM:Downloads:Helm:Linux", "helm.tar.gz", stoppingToken);
                }

                // Extract the assets.
                await _assetManager.ExtractAsset("cni-plugins.tar.gz", Path.Combine(_pathProvider.RKMRoot, "cni-plugins"), stoppingToken);

                // On the controller, extract the controller-specific assets.
                if (context.Role == RoleType.Controller)
                {
                    await _assetManager.ExtractAsset("helm.tar.gz", Path.Combine(_pathProvider.RKMRoot, "helm-bin"), stoppingToken, "linux-amd64");
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
