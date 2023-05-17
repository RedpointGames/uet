namespace Redpoint.UET.BuildPipeline.Executors.Engine
{
    using Redpoint.UET.Workspace;
    using System.Threading.Tasks;

    internal interface IEngineWorkspaceProvider
    {
        Task<IWorkspace> GetEngineWorkspace(BuildEngineSpecification buildEngineSpecification, string workspaceSuffix, CancellationToken cancellationToken);
    }
}
