namespace Redpoint.KubernetesManager.Components.DownstreamService
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.ServiceControl;

    /// <summary>
    /// The containerd component sets up and runs the containerd process.
    /// </summary>
    internal class ContainerdComponent : DownstreamComponent
    {
        public ContainerdComponent(
            IServiceControl serviceControl,
            IRkmVersionProvider rkmVersionProvider,
            IPathProvider pathProvider,
            ILogger<ContainerdComponent> logger,
            IHostApplicationLifetime hostApplicationLifetime)
            : base(
                serviceControl,
                rkmVersionProvider,
                pathProvider,
                logger,
                hostApplicationLifetime)
        {
        }

        protected override string ServiceName => "rkm-containerd";

        protected override string ServiceDescription => "RKM - Containerd";

        protected override string RunCommand => "run-containerd";

        protected override string ManifestFileName => "containerd-manifest.json";

        protected override string DisplayName => "containerd";
    }
}
