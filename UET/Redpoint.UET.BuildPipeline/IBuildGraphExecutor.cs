namespace Redpoint.UET.BuildPipeline
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IBuildGraphExecutor
    {
        Task<int> ExecuteGraphAsync(
            string enginePath,
            string buildGraphScriptPath,
            string buildGraphTarget,
            IEnumerable<string> buildGraphArguments,
            CancellationToken cancellationToken);
    }
}
