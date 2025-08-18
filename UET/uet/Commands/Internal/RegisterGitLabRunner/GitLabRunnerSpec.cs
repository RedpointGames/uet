namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using System.Text.Json.Serialization;

    internal class GitLabRunnerSpec
    {
        /// <summary>
        /// One of: "instance_type", "group_type" or "project_type".
        /// </summary>
        [JsonPropertyName("runner_type"), JsonRequired]
        public required string RunnerType { get; set; }

        [JsonPropertyName("group_id")]
        public int? GroupId { get; set; }

        [JsonPropertyName("project_id")]
        public int? ProjectId { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("paused")]
        public bool Paused { get; set; }

        [JsonPropertyName("locked")]
        public bool Locked { get; set; }

        [JsonPropertyName("run_untagged")]
        public bool RunUntagged { get; set; }

        [JsonPropertyName("tag_list")]
        public string? TagList { get; set; }

        /// <summary>
        /// One of: "not_protected", "ref_protected".
        /// </summary>
        [JsonPropertyName("access_level")]
        public string? AccessLevel { get; set; }

        [JsonPropertyName("maximum_timeout")]
        public int? MaximumTimeout { get; set; }

        [JsonPropertyName("maintenance_note")]
        public string? MaintainenceNote { get; set; }
    }
}
