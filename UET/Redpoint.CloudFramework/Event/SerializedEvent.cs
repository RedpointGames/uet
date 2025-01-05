namespace Redpoint.CloudFramework.Event
{
    using Google.Cloud.Datastore.V1;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NodaTime;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository;

    public class SerializedEvent
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("utcTimestamp")]
        public long UtcTimestamp { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("service")]
        public string? Service { get; set; }

        [JsonProperty("project")]
        public string? Project { get; set; }

        [JsonProperty("session")]
        public string? Session { get; set; }

        [JsonProperty("req")]
        public JObject? Request { get; set; }

        [JsonProperty("key")]
        public string? Key { get; set; }

        [JsonProperty("entity")]
        public JObject? Entity { get; set; }

        [JsonProperty("userdata")]
        public JObject? Userdata { get; set; }

        public static Event Deserialize(IGlobalPrefix globalPrefix, SerializedEvent jsonObject)
        {
            ArgumentNullException.ThrowIfNull(globalPrefix);
            ArgumentNullException.ThrowIfNull(jsonObject);

            var baseNamespace = string.Empty;
            Key? projectKey = null;
            Key? sessionKey = null;
            Key? objectKey = null;
            if (jsonObject.Project != null)
            {
                projectKey = globalPrefix.ParseInternal(string.Empty, (string)jsonObject.Project);
                baseNamespace = "proj_" + projectKey.GetIdFromKey();
            }
            if (jsonObject.Session != null)
            {
                sessionKey = globalPrefix.ParseInternal(baseNamespace, (string)jsonObject.Session);
            }
            if (jsonObject.Key != null)
            {
                objectKey = globalPrefix.ParseInternal(baseNamespace, (string)jsonObject.Key);
            }

            return new Event
            {
                Id = globalPrefix.ParseInternal(baseNamespace, (string)jsonObject.Id),
                UtcTimestamp = Instant.FromUnixTimeSeconds((long)jsonObject.UtcTimestamp),
                EventType = (string?)jsonObject.Type,
                ServiceIdentifier = (string?)jsonObject.Service,
                Project = projectKey,
                Session = sessionKey,
                Request = (JObject?)jsonObject.Request,
                Key = objectKey,
                Entity = (JObject?)jsonObject.Entity,
                Userdata = (JObject?)jsonObject.Userdata
            };
        }
    }
}
