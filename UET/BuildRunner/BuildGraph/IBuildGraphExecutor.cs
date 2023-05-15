namespace BuildRunner.BuildGraph
{
    using BuildRunner.BuildGraph.Environment;
    using BuildRunner.Configuration;
    using BuildRunner.Configuration.Engine;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IBuildGraphExecutor
    {
        Task<int> ExecuteGraphAsync(
            string enginePath,
            string buildGraphScriptPath,
            string buildGraphTarget,
            IEnumerable<string> buildGraphArguments,
            CancellationToken cancellationToken);
    }
}
