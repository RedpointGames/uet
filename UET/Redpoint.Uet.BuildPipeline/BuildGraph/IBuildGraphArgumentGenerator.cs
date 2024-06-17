namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    public interface IBuildGraphArgumentGenerator
    {
        IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            BuildGraphArgumentContext argumentContext);

        IReadOnlyDictionary<string, string> GeneratePreBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            BuildGraphArgumentContext argumentContext);
    }
}
