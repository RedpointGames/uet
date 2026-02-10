using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class CommitJson
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? Url { get; set; }

        [JsonPropertyName("author")]
        public AuthorInfo? Author { get; set; }
    }
}
