using Microsoft.Extensions.DependencyInjection;
using Redpoint.KubernetesManager.Components.ControllerOnly;
using Redpoint.KubernetesManager.Components.NodeOnly;
using Redpoint.KubernetesManager.Components.WslExtra;
using Redpoint.KubernetesManager.Components;
using Redpoint.KubernetesManager.Services.Windows;
using Redpoint.KubernetesManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Redpoint.KubernetesManager.Implementations;
using Redpoint.Windows.HostNetworkingService;
using Redpoint.Windows.Firewall;
using Redpoint.KubernetesManager.Services.Helm;

namespace Redpoint.KubernetesManager
{
    public static class KubernetesManagerServiceExtensions
    {
        public static void AddKubernetesManager(this IServiceCollection services)
        {
            // Register shared services.
            services.AddSingleton<IAssetConfiguration, DefaultAssetConfiguration>();
            services.AddSingleton<IPathProvider, DefaultPathProvider>();
            services.AddSingleton<IProcessKiller, DefaultProcessKiller>();
            services.AddSingleton<IAssetManager, DefaultAssetManager>();
            services.AddSingleton<IWindowsFeatureManager, WindowsFeatureManager>();
            services.AddSingleton<IResourceManager, DefaultResourceManager>();
            services.AddSingleton<ICertificateGenerator, DefaultCertificateGenerator>();
            services.AddSingleton<ICertificateManager, DefaultCertificateManager>();
            services.AddSingleton<ILocalEthernetInfo, DefaultLocalEthernetInfo>();
            services.AddSingleton<IKubeConfigGenerator, DefaultKubeConfigGenerator>();
            services.AddSingleton<IKubeConfigManager, DefaultKubeConfigManager>();
            services.AddSingleton<IEncryptionConfigManager, DefaultEncryptionConfigManager>();
            services.AddSingleton<IProcessMonitorFactory, DefaultProcessMonitorFactory>();
            services.AddSingleton<IKubernetesClientFactory, DefaultKubernetesClientFactory>();
            services.AddSingleton<IControllerAutodiscoveryService, DefaultControllerAutodiscoveryService>();
            services.AddSingleton<IControllerApiService, DefaultControllerApiService>();
            services.AddSingleton<INodeManifestClient, DefaultNodeManifestClient>();
            services.AddSingleton<IClusterNetworkingConfiguration, DefaultClusterNetworkingConfiguration>();
            services.AddSingleton<IWslDistro, DefaultWslDistro>();
            services.AddSingleton<IWslTranslation, DefaultWslTranslation>();
            services.AddSingleton<IRkmGlobalRootProvider, DefaultRkmGlobalRootProvider>();
            services.AddSingleton<IHelmDeployment, DefaultHelmDeployment>();

            // Register controller-only components.
            services.AddSingleton<IComponent, CertificateGeneratingComponent>();
            services.AddSingleton<IComponent, HelmCoreDNSProvisioningComponent>();
            services.AddSingleton<IComponent, EncryptionConfigGeneratingComponent>();
            services.AddSingleton<IComponent, EtcdComponent>();
            services.AddSingleton<IComponent, HelmRKMProvisioningComponent>();
            services.AddSingleton<IComponent, KubeConfigGeneratingComponent>();
            services.AddSingleton<IComponent, KubernetesApiServerComponent>();
            services.AddSingleton<IComponent, KubernetesClientComponent>();
            services.AddSingleton<IComponent, KubernetesControllerManagerComponent>();
            services.AddSingleton<IComponent, KubernetesResourceProvisioningComponent>();
            services.AddSingleton<IComponent, KubernetesSchedulerComponent>();
            services.AddSingleton<IComponent, NodeComponentGuardComponent>();
            services.AddSingleton<IComponent, RKMApiServiceStartingComponent>();

            // Register node-only components.
            services.AddSingleton<IComponent, NodeManifestExpanderComponent>();

            // Register WSL "extra" components.
            services.AddSingleton<IComponent, WslContainerdComponent>();
            services.AddSingleton<IComponent, WslKubeletComponent>();
            services.AddSingleton<IComponent, WslKubeProxyComponent>();
            services.AddSingleton<IComponent, CoreDNSComponent>();

            // Register shared components.
            services.AddSingleton<IComponent, AssetPreparationComponent>();
            services.AddSingleton<IComponent, ContainerdComponent>();
            services.AddSingleton<IComponent, KubeletComponent>();
            services.AddSingleton<IComponent, NetworkingConfigurationComponent>();

            // Register platform-specific components.
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IComponent, WindowsPreflightComponent>();
                services.AddSingleton(_ =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        return IHnsApi.GetInstance();
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }
                });
                services.AddSingleton(_ =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        return IWindowsFirewall.GetInstance();
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }
                });
                services.AddSingleton<IWindowsHcsService, DefaultWindowsHcsService>();
                services.AddSingleton<INetworkingConfiguration, WindowsNetworkingConfiguration>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IComponent, SwapDisablingComponent>();
                services.AddSingleton<INetworkingConfiguration, LinuxNetworkingConfiguration>();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Register command line arguments.
            services.AddSingleton<RKMCommandLineArguments>();

            // Register the main worker which executes components.
            services.AddSingleton<IComponent[]>(sp => sp.GetServices<IComponent>().ToArray());
            services.AddHostedService<RKMWorker>();
        }
    }
}
