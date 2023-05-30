namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    public class GitLabJob
    {
        [YamlMember(Alias = "stage", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Stage { get; set; } = null;

        [YamlMember(Alias = "needs", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public HashSet<string>? Needs { get; set; } = null;

        [YamlMember(Alias = "tags", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string[]? Tags { get; set; } = null;

        [YamlMember(Alias = "interruptible", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public bool? Interruptible { get; set; } = null;

        [YamlMember(Alias = "rules", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public GitLabJobRule[]? Rules { get; set; } = null;

        [YamlMember(Alias = "artifacts", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public GitLabJobArtifacts? Artifacts { get; set; } = null;

        [YamlMember(Alias = "script", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Script { get; set; } = null;

        [YamlMember(Alias = "after_script", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string[]? AfterScript { get; set; } = null;

        [YamlMember(Alias = "variables", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Dictionary<string, string>? Variables { get; set; } = null;
    }
}