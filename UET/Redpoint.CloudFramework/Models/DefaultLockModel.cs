namespace Redpoint.CloudFramework.Models
{
    using NodaTime;
    using System.Collections.Generic;

    [Kind("Lock")]
    public class DefaultLockModel : Model<DefaultLockModel>
    {
        // The lock key is part of the name in the key.
        [Type(FieldType.Timestamp), Indexed]
        public Instant? dateExpiresUtc { get; set; }

        [Type(FieldType.String), Indexed]
        public string? acquisitionGuid { get; set; }
    }
}
