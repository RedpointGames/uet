namespace Redpoint.Uet.BuildPipeline.BuildGraph.PreBuild
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public interface IPreBuild
    {
        Task<int> RunGeneralPreBuild(
            string repositoryRoot,
            string nodeName,
            IReadOnlyDictionary<string, string> preBuildGraphArguments,
            CancellationToken cancellationToken);
    }
}
