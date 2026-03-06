namespace Redpoint.KubernetesManager.Components.DownstreamService
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.ServiceControl;

    /// <summary>
    /// The kubelet component sets up and runs the kubelet process.
    /// </summary>
    internal class KubeletComponent : DownstreamComponent
    {
        public KubeletComponent(
            IServiceControl serviceControl,
            IRkmVersionProvider rkmVersionProvider,
            IPathProvider pathProvider,
            ILogger<KubeletComponent> logger,
            IHostApplicationLifetime hostApplicationLifetime)
            : base(
                serviceControl,
                rkmVersionProvider,
                pathProvider,
                logger,
                hostApplicationLifetime)
        {
        }

        protected override string ServiceName => "rkm-kubelet";

        protected override string ServiceDescription => "RKM - Kubelet";

        protected override string RunCommand => "run-kubelet";

        protected override string ManifestFileName => "kubelet-manifest.json";

        protected override string DisplayName => "kubelet";
    }
}
