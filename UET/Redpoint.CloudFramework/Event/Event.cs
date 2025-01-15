namespace Redpoint.CloudFramework.Event
{
    using Google.Cloud.Datastore.V1;
    using Newtonsoft.Json.Linq;
    using NodaTime;

#pragma warning disable CA1724
    public class Event
#pragma warning restore CA1724
    {
        public required Key Id { get; set; }

        public required Instant UtcTimestamp { get; set; }

        public string? EventType { get; set; }

        public string? ServiceIdentifier { get; set; }

        public Key? Project { get; set; }

        public Key? Session { get; set; }

        public JObject? Request { get; set; }

        public Key? Key { get; set; }

        public JObject? Entity { get; set; }

        public JObject? Userdata { get; set; }
    }
}
