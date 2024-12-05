namespace Redpoint.Uet.BuildPipeline.BuildGraph.Gradle
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IGradleWorkspace
    {
        Task<GradleWorkspaceInstance> GetGradleWorkspaceInstance(CancellationToken cancellationToken);
    }
}
