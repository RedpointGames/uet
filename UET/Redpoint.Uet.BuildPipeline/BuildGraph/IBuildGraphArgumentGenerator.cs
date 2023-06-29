namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    internal interface IBuildGraphArgumentGenerator
    {
        IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            string repositoryRoot,
            string uetPath,
            string enginePath,
            string sharedStoragePath,
            string artifactExportPath);
    }
}
