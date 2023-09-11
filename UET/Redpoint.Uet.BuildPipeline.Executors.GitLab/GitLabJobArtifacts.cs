namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using System.Diagnostics.CodeAnalysis;
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobArtifacts
    {
        [YamlMember(Alias = "when", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? When { get; set; } = null;

        [YamlMember(Alias = "paths", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for YAML serialization.")]
        public string[]? Paths { get; set; } = null;

        [YamlMember(Alias = "reports", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public GitLabJobArtifactsReports? Reports { get; set; } = null;
    }
}