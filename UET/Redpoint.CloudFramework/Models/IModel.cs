namespace Redpoint.CloudFramework.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using System.Collections.Generic;
    using System.Reflection;

    public interface IModel
    {
        Instant? dateCreatedUtc { get; internal set; }
        Instant? dateModifiedUtc { get; internal set; }
        long? schemaVersion { get; set; }

        internal Dictionary<string, object?>? _originalData { get; set; }

        string GetDatastoreNamespaceForLocalKeys();
    }
}
