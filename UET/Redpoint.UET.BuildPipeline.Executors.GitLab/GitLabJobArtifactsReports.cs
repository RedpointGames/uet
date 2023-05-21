namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    public class GitLabJobArtifactsReports
    {
        [YamlMember(Alias = "junit", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Junit { get; set; } = null;
    }
}