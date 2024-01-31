namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using System.Diagnostics.CodeAnalysis;
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJob
    {
        [YamlMember(Alias = "stage", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? Stage { get; set; } = null;

        [YamlMember(Alias = "needs", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This property is used for YAML serialization.")]
        public List<string>? Needs { get; set; } = null;

        [YamlMember(Alias = "tags", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This property is used for YAML serialization.")]
        public List<string>? Tags { get; set; } = null;

        [YamlMember(Alias = "interruptible", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public bool? Interruptible { get; set; } = null;

        [YamlMember(Alias = "rules", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for YAML serialization.")]
        public GitLabJobRule[]? Rules { get; set; } = null;

        [YamlMember(Alias = "artifacts", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public GitLabJobArtifacts? Artifacts { get; set; } = null;

        [YamlMember(Alias = "script", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for YAML serialization.")]
        public string[]? Script { get; set; } = null;

        [YamlMember(Alias = "after_script", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for YAML serialization.")]
        public string[]? AfterScript { get; set; } = null;

        [YamlMember(Alias = "variables", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public Dictionary<string, string>? Variables { get; set; } = null;
    }
}