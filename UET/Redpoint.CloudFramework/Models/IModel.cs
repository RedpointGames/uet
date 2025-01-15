namespace Redpoint.CloudFramework.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public interface IModel
    {
        Key Key { get; set; }
        Instant? dateCreatedUtc { get; internal set; }
        Instant? dateModifiedUtc { get; internal set; }
        long? schemaVersion { get; set; }

        internal Dictionary<string, object?>? _originalData { get; set; }

        internal string GetKind();
        internal Dictionary<string, FieldType> GetTypes();
        internal HashSet<string> GetIndexes();
        internal Dictionary<string, object>? GetDefaultValues();
        internal PropertyInfo[] GetPropertyInfos();
        internal PropertyInfo? GetPropertyInfo(string name);

        long GetSchemaVersion();
        string GetDatastoreNamespaceForLocalKeys();
    }
}
