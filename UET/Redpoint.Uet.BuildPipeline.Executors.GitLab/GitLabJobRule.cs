namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobRule
    {
        [YamlMember(Alias = "if", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? If { get; set; } = null;

        [YamlMember(Alias = "when", DefaultValuesHandling = DefaultValuesHandling.OmitNull, ScalarStyle = YamlDotNet.Core.ScalarStyle.DoubleQuoted)]
        public string? When { get; set; } = null;
    }
}