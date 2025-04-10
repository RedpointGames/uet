﻿namespace Redpoint.Uet.BuildPipeline.BuildGraph
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
                value = value.Replace("__REPOSITORY_ROOT__", repositoryRoot, StringComparison.Ordinal);
                value = value.Replace("__UET_PATH__", uetPath, StringComparison.Ordinal);
                value = value.Replace("__ENGINE_PATH__", enginePath.TrimEnd('\\'), StringComparison.Ordinal);
                value = value.Replace(
                    "__UNREAL_ENGINE_IS_INSTALLED_BUILD__",
                    File.Exists(Path.Combine(enginePath, "Engine", "Build", "InstalledBuild.txt")) ? "true" : "false",
                    StringComparison.Ordinal);
                value = value.Replace("__SHARED_STORAGE_PATH__", sharedStoragePath, StringComparison.Ordinal);
                value = value.Replace("__ARTIFACT_EXPORT_PATH__", artifactExportPath, StringComparison.Ordinal);
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
            string repositoryRoot,
            string uetPath,
            string enginePath,
            string sharedStoragePath,
            string artifactExportPath)
        {
            var results = new Dictionary<string, string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
                value = value.Replace("__REPOSITORY_ROOT__", repositoryRoot, StringComparison.Ordinal);
                value = value.Replace("__UET_PATH__", uetPath, StringComparison.Ordinal);
                value = value.Replace("__ENGINE_PATH__", enginePath.TrimEnd('\\'), StringComparison.Ordinal);
                value = value.Replace(
                    "__UNREAL_ENGINE_IS_INSTALLED_BUILD__",
                    File.Exists(Path.Combine(enginePath, "Engine", "Build", "InstalledBuild.txt")) ? "true" : "false",
                    StringComparison.Ordinal);
                value = value.Replace("__SHARED_STORAGE_PATH__", sharedStoragePath, StringComparison.Ordinal);
                value = value.Replace("__ARTIFACT_EXPORT_PATH__", artifactExportPath, StringComparison.Ordinal);
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
