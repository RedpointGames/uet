namespace Redpoint.KubernetesManager.Signalling
{
    using Redpoint.KubernetesManager.Services.Windows;

    internal static class WellKnownFlags
    {
        /// <summary>
        /// This flag is set once the node has it's manifest from the controller and
        /// the controller address, both of which are stored in the <see cref="Data.NodeContextData"/>
        /// which is associated with the flag.
        /// </summary>
        public const string NodeContextAvailable = "node-context-ready";

        /// <summary>
        /// This flag is set once the operating system has been configured for
        /// Kubernetes networking. If you need to access <see cref="IWslDistro.GetWslDistroIPAddress(CancellationToken)"/>,
        /// you must wait on this flag first to make sure WSL has the correct IP address.
        /// </summary>
        public const string OSNetworkingReady = "os-networking-ready";

        /// <summary>
        /// This flag is set once the certificates required for all the
        /// relevant Kubernetes components are ready on disk.
        /// </summary>
        public const string CertificatesReady = "certificates-ready";

        /// <summary>
        /// This flag is set once the kubeconfigs required for all the
        /// relevant Kubernetes components are ready on disk.
        /// </summary>
        public const string KubeConfigsReady = "kubeconfigs-ready";

        /// <summary>
        /// This flag is set once the "encryption-config.yaml" file required
        /// required by the API server is generated on disk.
        /// </summary>
        public const string EncryptionConfigReady = "encryption-config-ready";

        /// <summary>
        /// This flag is set once the <see cref="Components.AssetPreparationComponent"/>
        /// has downloaded and extracted all required assets.
        /// </summary>
        public const string AssetsReady = "assets-ready";

        /// <summary>
        /// This flag is set once the kube-apiserver process has started. Note that
        /// this doesn't mean the API server is ready to serve requests yet though;
        /// for that you should use <see cref="KubeApiServerReady"/>. Only available
        /// on the controller.
        /// </summary>
        public const string KubeApiServerStarted = "kube-apiserver-started";

        /// <summary>
        /// This flag is set once the kube-apiserver process is ready to serve requests.
        /// The <see cref="Components.ControllerOnly.KubernetesClientComponent"/> sets this 
        /// flag once it's been able to connect to the API server. Only available
        /// on the controller. The <see cref="k8s.IKubernetes"/> object is available via
        /// the <see cref="Data.KubernetesClientContextData"/> on the flag.
        /// </summary>
        public const string KubeApiServerReady = "kube-apiserver-ready";

        /// <summary>
        /// This flag is set once the RKM components Helm chart has been deployed to the cluster.
        /// </summary>
        public const string HelmChartProvisioned = "helm-chart-provisioned";

        /// <summary>
        /// This flag is set once the node components are ready to start. On the controller, this
        /// is only set after the API server is started and all the core networking infrastructure
        /// is provisioned. On a non-controller, this is set as soon as the certificates and
        /// kubeconfigs have been extracted by <see cref="Components.NodeOnly.NodeManifestExpanderComponent"/>.
        /// </summary>
        public const string NodeComponentsReadyToStart = "node-components-ready-to-start";

        /// <summary>
        /// This flag is set once the kubelet process has stopped during shutdown.
        /// </summary>
        public const string KubeletStopped = "kubelet-stopped";
    }
}
