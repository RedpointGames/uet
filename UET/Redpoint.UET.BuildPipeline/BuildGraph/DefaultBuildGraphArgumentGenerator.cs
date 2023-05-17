namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using System.Collections.Generic;

    internal class DefaultBuildGraphArgumentGenerator : IBuildGraphArgumentGenerator
    {
        public IEnumerable<string> GenerateBuildGraphArguments(
            Dictionary<string, string> arguments,
            Dictionary<string, string> replacements)
        {
            var results = new List<string>();
            foreach (var kv in arguments)
            {
                var value = kv.Value;
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
