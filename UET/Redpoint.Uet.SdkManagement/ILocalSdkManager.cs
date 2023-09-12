namespace Redpoint.Uet.SdkManagement
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ILocalSdkManager
    {
        Task<Dictionary<string, string>> SetupEnvironmentForBuildGraphNode(
            string enginePath,
            string sdksPath,
            string buildGraphNodeName,
            CancellationToken cancellationToken);

        Task<Dictionary<string, string>> SetupEnvironmentForSdkSetups(
            string enginePath,
            string sdksPath,
            ISet<ISdkSetup> sdkSetups,
            CancellationToken cancellationToken);
    }
}
