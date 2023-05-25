namespace Redpoint.UET.BuildPipeline.Executors.Engine
{
    using Redpoint.UET.Workspace;
    using System.Threading.Tasks;

    public interface IEngineWorkspaceProvider
    {
        Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            bool useStorageVirtualisation,
            CancellationToken cancellationToken);
    }
}
