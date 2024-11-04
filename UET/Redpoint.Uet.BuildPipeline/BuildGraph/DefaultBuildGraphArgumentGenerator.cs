namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    internal class DefaultBuildGraphArgumentGenerator : IBuildGraphArgumentGenerator
    {
        public IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            BuildGraphArgumentContext argumentContext)
        {
            var results = new List<string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
                value = value.Replace("__REPOSITORY_ROOT_OUTPUT__", argumentContext.RepositoryRoot.OutputPath, StringComparison.Ordinal);
                value = value.Replace("__REPOSITORY_ROOT_BASE_CODE__", argumentContext.RepositoryRoot.BaseCodePath, StringComparison.Ordinal);
                value = value.Replace("__REPOSITORY_ROOT_PLATFORM_CODE__", argumentContext.RepositoryRoot.PlatformCodePath, StringComparison.Ordinal);
                value = value.Replace("__UET_PATH__", argumentContext.UetPath, StringComparison.Ordinal);
                value = value.Replace("__ENGINE_PATH__", argumentContext.EnginePath.TrimEnd('\\'), StringComparison.Ordinal);
                value = value.Replace("__SHARED_STORAGE_PATH__", argumentContext.SharedStoragePath, StringComparison.Ordinal);
                value = value.Replace("__ARTIFACT_EXPORT_PATH__", argumentContext.ArtifactExportPath, StringComparison.Ordinal);
                foreach (var sr in replacements)
                {
                    value = value.Replace(sr.Key, sr.Value, StringComparison.Ordinal);
                }
                results.Add($"-set:{kv.Key}={value}");
            }
            return results;
        }

        public IReadOnlyDictionary<string, string> GeneratePreBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements,
            BuildGraphArgumentContext argumentContext)
        {
            var results = new Dictionary<string, string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
                value = value.Replace("__REPOSITORY_ROOT_OUTPUT__", argumentContext.RepositoryRoot.OutputPath, StringComparison.Ordinal);
                value = value.Replace("__REPOSITORY_ROOT_BASE_CODE__", argumentContext.RepositoryRoot.BaseCodePath, StringComparison.Ordinal);
                value = value.Replace("__REPOSITORY_ROOT_PLATFORM_CODE__", argumentContext.RepositoryRoot.PlatformCodePath, StringComparison.Ordinal);
                value = value.Replace("__UET_PATH__", argumentContext.UetPath, StringComparison.Ordinal);
                value = value.Replace("__ENGINE_PATH__", argumentContext.EnginePath.TrimEnd('\\'), StringComparison.Ordinal);
                value = value.Replace("__SHARED_STORAGE_PATH__", argumentContext.SharedStoragePath, StringComparison.Ordinal);
                value = value.Replace("__ARTIFACT_EXPORT_PATH__", argumentContext.ArtifactExportPath, StringComparison.Ordinal);
                foreach (var sr in replacements)
                {
                    value = value.Replace(sr.Key, sr.Value, StringComparison.Ordinal);
                }
                results.Add(kv.Key, value);
            }
            return results;
        }
    }
}
