namespace Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning
{
    using Redpoint.Uet.Configuration.Engine;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMobileProvisioning
    {
        Task InstallMobileProvisions(
            IEnumerable<BuildConfigMobileProvision> mobileProvisions,
            CancellationToken cancellationToken);
    }
}
