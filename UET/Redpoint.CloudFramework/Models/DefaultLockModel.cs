namespace Redpoint.CloudFramework.Models
{
    using NodaTime;
    using System.Collections.Generic;

    public class DefaultLockModel : Model
    {
        // The lock key is part of the name in the key.
        public Instant? dateExpiresUtc { get; set; }
        public string? acquisitionGuid { get; set; }

        public override string GetKind()
        {
            return "Lock";
        }

        public override long GetSchemaVersion()
        {
            return 1;
        }

        public override Dictionary<string, FieldType> GetTypes()
        {
            return new Dictionary<string, FieldType>
            {
                { "dateExpiresUtc", FieldType.Timestamp },
                { "acquisitionGuid", FieldType.String },
            };
        }

        public override HashSet<string> GetIndexes()
        {
            return new HashSet<string>
            {
                "dateExpiresUtc",
                "acquisitionGuid"
            };
        }
    }
}
