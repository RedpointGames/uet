namespace Redpoint.CloudFramework.Tests
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using System;

    [Kind<EmbeddedEntityModel>("cf_embeddedEntityModel")]
    public class EmbeddedEntityModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public string? forTest { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }

        [Type(FieldType.EmbeddedEntity), Indexed]
        public Entity? entity { get; set; }
    }
}
