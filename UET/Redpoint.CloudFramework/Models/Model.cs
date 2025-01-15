namespace Redpoint.CloudFramework.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using System;
    using System.Collections.Generic;

    public abstract class Model
    {
        // Declaring this field as nullable would make 99% of reading code
        // overly verbose handling scenarios that can never happen (it will never
        // be null for entities loaded from the database). The only time that
        // the key can be null is if you are creating an entity and haven't
        // called CreateAsync yet.
#pragma warning disable CS8618
        public Key Key { get; set; }
#pragma warning restore CS8618

        public Instant? dateCreatedUtc { get; internal set; }
        public Instant? dateModifiedUtc { get; internal set; }
        public long? schemaVersion { get; set; }

        /// <summary>
        /// The original entity when it was loaded; this is used to clear caches
        /// when appropriate.
        /// </summary>
        internal Dictionary<string, object?>? _originalData { get; set; }

        public abstract string GetKind();

        public abstract Dictionary<string, FieldType> GetTypes();

        public abstract HashSet<string> GetIndexes();

        public abstract long GetSchemaVersion();

        public virtual string GetDatastoreNamespaceForLocalKeys()
        {
            throw new NotSupportedException("This model has a property of type 'local-key', but does not implement GetDatastoreNamespaceForLocalKeys");
        }

        public virtual Dictionary<string, object>? GetDefaultValues()
        {
            return null;
        }
    }
}
