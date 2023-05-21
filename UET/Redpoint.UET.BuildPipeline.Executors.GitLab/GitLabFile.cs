namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using YamlDotNet.Serialization;

    public class GitLabFile
    {
        [YamlMember(Alias = "stages", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public List<string>? Stages { get; set; } = null;

        [YamlMember(Alias = "variables", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Dictionary<string, string>? Variables { get; set; } = null;

        public Dictionary<string, GitLabJob>? Jobs { get; set; } = null;
    }
}