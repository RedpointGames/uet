namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using Redpoint.Windows.HostNetworkingService;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal class CalicoWindowsComponent : IComponent
    {
        private readonly ILogger<CalicoWindowsComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHnsApi _hnsService;
        private bool _hasSetFlag;

        public CalicoWindowsComponent(
            ILogger<CalicoWindowsComponent> logger,
            IPathProvider pathProvider,
            IProcessMonitorFactory processMonitorFactory,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ILocalEthernetInfo localEthernetInfo,
            IHnsApi hnsService)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _processMonitorFactory = processMonitorFactory;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _localEthernetInfo = localEthernetInfo;
            _hnsService = hnsService;
            _hasSetFlag = false;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.KubeletProcessStartedOnWindows, OnKubeletProcessStartedAsync);
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        /// <summary>
        /// Both "calico-node -startup" and "calico-node -felix" need the same set of environment
        /// variables, so this function just lets us grab them in both places consistently.
        /// </summary>
        private Dictionary<string, string> GetCalicoEnvironmentVariables(string nodeName)
        {
            return new Dictionary<string, string>
            {
                { "CALICO_DATASTORE_TYPE", "kubernetes" },
                { "K8S_SERVICE_CIDR", _clusterNetworkingConfiguration.ServiceCIDR },
                { "DNS_NAME_SERVERS", _clusterNetworkingConfiguration.ClusterDNSServiceIP },
                { "DNS_SEARCH", $"svc.{_clusterNetworkingConfiguration.ClusterDNSDomain}" },
                { "KUBECONFIG", Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", "components", $"component-calico-windows-node.kubeconfig") },
                { "CALICO_NETWORKING_BACKEND", "vxlan" },
                { "CALICO_NODENAME_FILE", Path.Combine(_pathProvider.RKMRoot, "calico-windows", "nodename") },
                { "KUBE_NETWORK", "Calico.*" },
                { "CNI_BIN_DIR", Path.Combine(_pathProvider.RKMRoot, "cni-plugins") },
                { "CNI_CONF_DIR", Path.Combine(_pathProvider.RKMRoot, "containerd-state", "cni", "conf") },
                { "CNI_CONF_FILENAME", "10-calico.conf" },
                { "CNI_IPAM_TYPE", "calico-ipam" },
                { "VXLAN_VNI", _clusterNetworkingConfiguration.VXLANVNI.ToString(CultureInfo.InvariantCulture) },
                { "VXLAN_MAC_PREFIX", "0E-2A" },
                { "NODENAME", nodeName },
                { "CALICO_K8S_NODE_REF", nodeName },
                { "IP", _localEthernetInfo.IPAddress.ToString() },
                { "CALICO_LOG_DIR", Path.Combine(_pathProvider.RKMRoot, "logs", "calico") },
                // Felix specific settings that we set across both processes just to make
                // things easy.
                { "FELIX_FELIXHOSTNAME", nodeName },
                { "FELIX_METADATAADDR", "none" },
                { "FELIX_VXLANVNI", _clusterNetworkingConfiguration.VXLANVNI.ToString(CultureInfo.InvariantCulture) },
                { "USE_POD_CIDR", "false" },
            };
        }

        /// <summary>
        /// calico-node runs with a -startup parameter, configures a bunch of networking and then
        /// exits. The process doesn't stick around, which means it's not suitable for running inside
        /// the multi-process monitor that we use to e.g. make sure kubelet stays alive. Instead we call
        /// this function to run calico-node under two circumstances:
        /// 
        /// - after kubelet first starts
        /// - whenever kubelet restarts
        /// 
        /// It doesn't look like Windows uses the -shutdown parameter at any point like Linux does.
        /// </summary>
        private async Task OnKubeletProcessStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            string nodeName;
            if (context.Role == RoleType.Controller)
            {
                await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);
                await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerReady);
                await context.WaitForFlagAsync(WellKnownFlags.CalicoWindowsKubeConfigReady);

                // On a Windows controller, the node name is just the machine name.
                nodeName = Environment.MachineName.ToLowerInvariant();
            }
            else
            {
                // Get the node manifest (this should be instant since Calico for Windows won't be starting
                // until Kubelet does, which only happens well after the manifest is downloaded).
                var nodeContext = await context.WaitForFlagAsync<NodeContextData>(WellKnownFlags.NodeContextAvailable);
                nodeName = nodeContext.NodeManifest.NodeName;
            }

            // When calico-node runs after the kubelet start, if the kubelet hasn't run the node registration
            // yet, calico-node will fail to run. Therefore we have to retry it over the next minute to give
            // the kubelet time to register itself with the API server.
            for (int i = 0; i < 30; i++)
            {
                Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "logs", "calico"));
                var calicoProcess = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                    filename: Path.Combine(_pathProvider.RKMRoot, "calico-windows", "calico-node"),
                    arguments: new[]
                    {
                    "-startup",
                    },
                    environment: GetCalicoEnvironmentVariables(nodeName)));
                var calicoExitCode = await calicoProcess.RunAsync(cancellationToken);
                if (calicoExitCode != 0)
                {
                    if (i < 29)
                    {
                        _logger.LogWarning($"calico-node -startup failed with non-zero exit code {calicoExitCode}, treating as a temporary error and retrying in 1 second...");
                        await Task.Delay(1000, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError($"calico-node -startup exited with non-zero exit code {calicoExitCode}, which probably means the network on this machine will not be configured correctly!");
                    }
                }
                else
                {
                    // Allow further initialization to proceed in the main startup. felix and kube-proxy
                    // will be waiting on calico-node to have run for the first time.
                    if (!_hasSetFlag)
                    {
                        _hasSetFlag = true;
                        context.SetFlag(WellKnownFlags.CalicoWindowsConfiguredNetwork);
                    }
                    break;
                }
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            string nodeName;
            if (context.Role == RoleType.Controller)
            {
                await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);
                await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerReady);
                await context.WaitForFlagAsync(WellKnownFlags.CalicoWindowsKubeConfigReady);

                // On a Windows controller, the node name is just the machine name.
                nodeName = Environment.MachineName.ToLowerInvariant();
            }
            else
            {
                // Get the node manifest (this should be instant since Calico for Windows won't be starting
                // until Kubelet does, which only happens well after the manifest is downloaded).
                var nodeContext = await context.WaitForFlagAsync<NodeContextData>(WellKnownFlags.NodeContextAvailable);
                nodeName = nodeContext.NodeManifest.NodeName;
            }

            // Wait for calico-node to have run once before we start calico-node -felix.
            await context.WaitForFlagAsync(WellKnownFlags.CalicoWindowsConfiguredNetwork);

            // Now start calico-node -felix, which performs endpoint management and
            // routing configuration as needed.
            _logger.LogInformation("Starting node-calico -felix...");
            var felixMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "calico-windows", "calico-node"),
                arguments: new[]
                {
                    "-felix",
                },
                environment: GetCalicoEnvironmentVariables(nodeName)));
            var felixProcess = felixMonitor.RunAsync(cancellationToken);

            try
            {
                // Now wait until our calico HNS network is available. We need it to be up
                // and running so we can get the parameters to launch kube-proxy.
                string hnsNetworkAddressPrefix = string.Empty;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var newHnsNetwork = _hnsService.GetHnsNetworks().FirstOrDefault(x => x.Name == _clusterNetworkingConfiguration.HnsNetworkName);
                    if (newHnsNetwork == null)
                    {
                        _logger.LogInformation($"calico has not created HNS network for {_clusterNetworkingConfiguration.HnsNetworkName} yet, waiting so we can get the address for kube-proxy...");
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    hnsNetworkAddressPrefix = newHnsNetwork.Subnets[0].AddressPrefix!;
                    break;
                }
                _logger.LogInformation($"calico is using the following address prefix inside {_clusterNetworkingConfiguration.HnsNetworkName}: {hnsNetworkAddressPrefix}");

                // Also wait for the Calico HNS endpoint to be available. This is created
                // by Calico and used as the --source-vip parameter for kube-proxy.
                string sourceVip = string.Empty;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var newHnsEndpoint = _hnsService.GetHnsEndpoints().FirstOrDefault(x => x.Name == _clusterNetworkingConfiguration.HnsEndpointName);
                    if (newHnsEndpoint == null)
                    {
                        _logger.LogInformation($"calico has not created HNS endpoint for {_clusterNetworkingConfiguration.HnsEndpointName} yet, waiting so we can get the address for kube-proxy...");
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    sourceVip = newHnsEndpoint.IPAddress!;
                    break;
                }
                _logger.LogInformation($"calico is using the following address for endpoint {_clusterNetworkingConfiguration.HnsEndpointName}: {sourceVip}");

                context.SetFlag(WellKnownFlags.CalicoWindowsReady, new CalicoWindowsContextData(sourceVip));
            }
            finally
            {
                // Wait for the felix processes to be cleaned up on shutdown before returning.
                await felixProcess;
            }
        }
    }
}
