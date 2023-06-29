namespace Redpoint.Uet.Workspace.Credential
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class DockerConfigJson
    {
        internal class DockerAuthSetting
        {
            [JsonPropertyName("auth")]
            public string Auth { get; set; } = string.Empty;
        }

        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthSetting> Auths { get; set; } = new Dictionary<string, DockerAuthSetting>();

        [JsonPropertyName("credsStore")]
        public string CredsStore { get; set; } = string.Empty;
    }
}
