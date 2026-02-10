namespace Io.Json.Frontend
{
    using System.Text.Json.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class TestStats
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("startedUtcMillis")]
        public long? StartedUtcMillis { get; set; }

        [JsonPropertyName("finishedUtcMillis")]
        public long? FinishedUtcMillis { get; set; }
    }
}
