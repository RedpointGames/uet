namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    internal class DefaultBuildGraphArgumentGenerator : IBuildGraphArgumentGenerator
    {
        public IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            string repositoryRoot,
            string uetPath,
            string enginePath,
            string sharedStoragePath,
            string artifactExportPath)
        {
            var results = new List<string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
                value = value.Replace("__REPOSITORY_ROOT__", repositoryRoot);
                value = value.Replace("__UET_PATH__", uetPath);
                value = value.Replace("__ENGINE_PATH__", enginePath);
                value = value.Replace("__SHARED_STORAGE_PATH__", sharedStoragePath);
                value = value.Replace("__ARTIFACT_EXPORT_PATH__", artifactExportPath);
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
