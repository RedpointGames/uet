namespace Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning
{
    using Redpoint.Uet.Configuration.Engine;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class NullMobileProvisioning : IMobileProvisioning
    {
        public Task InstallMobileProvisions(
            string enginePath,
            bool isEngineBuild,
            IEnumerable<BuildConfigMobileProvision> mobileProvisions,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
