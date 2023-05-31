namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using System.Diagnostics.CodeAnalysis;
    using YamlDotNet.Serialization;

    [YamlSerializable]
    public class GitLabJobRule
    {
        [YamlMember(Alias = "if", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? If { get; set; } = null;

        [YamlMember(Alias = "when", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? When { get; set; } = null;
    }
}