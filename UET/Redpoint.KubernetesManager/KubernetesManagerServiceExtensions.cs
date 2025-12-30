namespace Redpoint.KubernetesManager
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Components;
    using Redpoint.KubernetesManager.Components.ControllerOnly;
    using Redpoint.KubernetesManager.Components.DownstreamService;
    using Redpoint.KubernetesManager.Components.NodeOnly;
    using Redpoint.KubernetesManager.ControllerApi;
    using Redpoint.KubernetesManager.Implementations;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.PerpetualProcess;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Helm;
    using Redpoint.KubernetesManager.Services.Linux;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Services.Wsl;
    using Redpoint.Windows.Firewall;
    using Redpoint.Windows.HostNetworkingService;

    public static class KubernetesManagerServiceExtensions
    {
        public static void AddKubernetesManager(this IServiceCollection services, bool withPathProvider)
        {
            // Register shared services.
            services.AddSingleton<IAssetConfiguration, DefaultAssetConfiguration>();
            if (withPathProvider)
            {
                services.AddSingleton<IPathProvider, DefaultPathProvider>();
                services.AddSingleton<IAssetManager, DefaultAssetManager>();
                services.AddSingleton<IWslDistro, DefaultWslDistro>();
            }
            services.AddSingleton<IWindowsFeatureManager, WindowsFeatureManager>();
            services.AddSingleton<ICertificateGenerator, DefaultCertificateGenerator>();
            services.AddSingleton<ICertificateManager, DefaultCertificateManager>();
            services.AddSingleton<ILocalEthernetInfo, DefaultLocalEthernetInfo>();
            services.AddSingleton<IKubeconfigGenerator, DefaultKubeconfigGenerator>();
            services.AddSingleton<IEncryptionConfigManager, DefaultEncryptionConfigManager>();
            services.AddSingleton<IControllerAutodiscoveryService, DefaultControllerAutodiscoveryService>();
            services.AddSingleton<INodeManifestClient, DefaultNodeManifestClient>();
            services.AddSingleton<IClusterNetworkingConfiguration, DefaultClusterNetworkingConfiguration>();
            services.AddSingleton<IWslTranslation, DefaultWslTranslation>();
            services.AddSingleton<IRkmGlobalRootProvider, DefaultRkmGlobalRootProvider>();
            services.AddSingleton<IHelmDeployment, DefaultHelmDeployment>();
            services.AddSingleton<ITpmService, DefaultTpmService>();

            services.AddRkmManifest();
            services.AddRkmPerpetualProcess();
            services.AddKestrelFactory();

            services.AddSingleton<IControllerEndpoint, GetLegacyManifestControllerEndpoint>();
            services.AddSingleton<IControllerEndpoint, GetNodeManifestControllerEndpoint>();
            services.AddSingleton<IControllerEndpoint, PutNodeAuthorizeControllerEndpoint>();

            // Register controller-only components.
            if (withPathProvider)
            {
                services.AddSingleton<IComponent, CertificateGeneratingComponent>();
                services.AddSingleton<IComponent, ControllerManifestServerComponent>();
                services.AddSingleton<IComponent, EncryptionConfigGeneratingComponent>();
                services.AddSingleton<IComponent, HelmRKMProvisioningComponent>();
                services.AddSingleton<IComponent, KubeconfigGeneratingComponent>();
                services.AddSingleton<IComponent, NodeComponentGuardComponent>();
                services.AddSingleton<IComponent, WaitForApiServerReadyOnControllerComponent>();

                // Register node-only components.
                services.AddSingleton<IComponent, NodeManifestExpanderComponent>();

                // Register shared components.
                services.AddSingleton<IComponent, AssetPreparationComponent>();
                services.AddSingleton<IComponent, ContainerdComponent>();
                services.AddSingleton<IComponent, NodeManifestServerComponent>();
                services.AddSingleton<IComponent, KubeletComponent>();
                services.AddSingleton<IComponent, NetworkingConfigurationComponent>();
            }

            // Register platform-specific components.
            if (OperatingSystem.IsWindows())
            {
                if (withPathProvider)
                {
                    services.AddSingleton<IComponent, WindowsPreflightComponent>();
                }
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
                services.AddSingleton<INetworkingConfiguration, LinuxNetworkingConfiguration>();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (withPathProvider)
            {
                // Register command line arguments.
                services.AddSingleton<RKMCommandLineArguments>();

                // Register the main worker which executes components.
                services.AddSingleton<IComponent[]>(sp => sp.GetServices<IComponent>().ToArray());
            }
        }
    }
}
