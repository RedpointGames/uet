namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    public class GitLabJobArtifacts
    {
        [YamlMember(Alias = "when", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? When { get; set; } = null;

        [YamlMember(Alias = "paths", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string[]? Paths { get; set; } = null;

        [YamlMember(Alias = "reports", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public GitLabJobArtifactsReports? Reports { get; set; } = null;
    }
}