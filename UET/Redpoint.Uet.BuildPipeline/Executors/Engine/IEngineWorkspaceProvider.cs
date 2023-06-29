namespace Redpoint.Uet.BuildPipeline.Executors.Engine
{
    using Redpoint.Uet.Workspace;
    using System.Threading.Tasks;

    public interface IEngineWorkspaceProvider
    {
        Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            CancellationToken cancellationToken);
    }
}
