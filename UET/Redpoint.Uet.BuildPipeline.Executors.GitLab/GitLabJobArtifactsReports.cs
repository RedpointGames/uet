namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobArtifactsReports
    {
        [YamlMember(Alias = "junit", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? Junit { get; set; } = null;
    }
}