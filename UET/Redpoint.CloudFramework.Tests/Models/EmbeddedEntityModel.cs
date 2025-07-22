namespace Redpoint.CloudFramework.Tests.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using System;

    [Kind("cf_embeddedEntityModel")]
    public sealed class EmbeddedEntityModel : Model<EmbeddedEntityModel>
    {
        [Type(FieldType.String), Indexed]
        public string? forTest { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }

        [Type(FieldType.EmbeddedEntity), Indexed]
        public Entity? entity { get; set; }
    }
}
