namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using System.Diagnostics.CodeAnalysis;
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobArtifactsReports
    {
        [YamlMember(Alias = "junit", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Junit { get; set; } = null;
    }
}