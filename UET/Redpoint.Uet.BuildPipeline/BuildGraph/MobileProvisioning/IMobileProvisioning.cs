namespace Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning
{
    using Redpoint.Uet.Configuration.Engine;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IMobileProvisioning
    {
        Task InstallMobileProvisions(
            string enginePath,
            bool isEngineBuild,
            IEnumerable<BuildConfigMobileProvision> mobileProvisions,
            CancellationToken cancellationToken);
    }
}
