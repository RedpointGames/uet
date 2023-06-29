namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobArtifacts
    {
        [YamlMember(Alias = "when", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? When { get; set; } = null;

        [YamlMember(Alias = "paths", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string[]? Paths { get; set; } = null;

        [YamlMember(Alias = "reports", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public GitLabJobArtifactsReports? Reports { get; set; } = null;
    }
}