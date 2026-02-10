using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class AuthorInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
