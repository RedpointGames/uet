namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    public interface IBuildGraphArgumentGenerator
    {
        IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            string repositoryRoot,
            string uetPath,
            string enginePath,
            string sharedStoragePath,
            string artifactExportPath);

        IReadOnlyDictionary<string, string> GeneratePreBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            string repositoryRoot,
            string uetPath,
            string enginePath,
            string sharedStoragePath,
            string artifactExportPath);
    }
}
