namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    internal class DefaultBuildGraphArgumentGenerator : IBuildGraphArgumentGenerator
    {
        public IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            string repositoryRoot,
            string uetPath,
            string enginePath)
        {
            var results = new List<string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
                value = value.Replace("__REPOSITORY_ROOT__", repositoryRoot);
                value = value.Replace("__UET_PATH__", uetPath);
                value = value.Replace("__ENGINE_PATH__", enginePath);
                foreach (var sr in replacements)
                {
                    value = value.Replace(sr.Key, sr.Value);
                }
                results.Add($"-set:{kv.Key}={value}");
            }
            return results;
        }
    }
}
