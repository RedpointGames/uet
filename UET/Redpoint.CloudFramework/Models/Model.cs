namespace Redpoint.CloudFramework.Models
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using Redpoint.CloudFramework.Repository.Geographic;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// A version of <c>Model</c> that you can inherit from, where the Datastore schema is defined
    /// by attributes on the class and properties instead of implementing the abstract <c>Model</c>
    /// methods.
    /// 
    /// Implements caching so that when the application has to determine the schema from the 
    /// model class, it's slightly faster than the naive implementation of returning newly
    /// constructed objects from the <c>Model</c> methods.
    /// </summary>
    public class Model<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : IModel, IGeoModel where T : Model<T>
    {
        private readonly ModelInfo _modelInfo;

        /// <summary>
        /// The key for the entity in the database.
        /// </summary>
        /// <remarks>
        /// Declaring this field as nullable would make 99% of reading code overly verbose handling scenarios that can never happen (it will never be null for entities loaded from the database). The only time that the key can be null is if you are creating an entity and haven't called CreateAsync yet.
        /// </remarks>
        public Key Key { get; set; }

        /// <summary>
        /// The date that the entity was created. Setting this field has no effect when updating entities.
        /// </summary>
        public Instant? dateCreatedUtc { get; set; }

        /// <summary>
        /// The date that the entity was last modified. Setting this field has no effect when updating entities.
        /// </summary>
        public Instant? dateModifiedUtc { get; set; }

        /// <summary>
        /// The schema version; this is used to detect when an entity requires a migration.
        /// </summary>
        public long? schemaVersion { get; set; }

        /// <summary>
        /// Tracks the original data when the entity was loaded so that caches can be correctly invalidated upon write.
        /// </summary>
        Dictionary<string, object?>? IModel._originalData { get; set; }

#pragma warning disable CS8618
        public Model()
#pragma warning restore CS8618
        {
            _modelInfo = ModelInfoRegistry.InitModel(this);
        }

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        string IModel.GetKind() => _modelInfo._kind;

        /// <remarks>
        /// This accessor is only for unit tests so they don't need to cast to IModel.
        /// </remarks>
        internal string GetKind() => _modelInfo._kind;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        HashSet<string> IModel.GetIndexes() => _modelInfo._indexes;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        Dictionary<string, object>? IModel.GetDefaultValues() => _modelInfo._defaultValues;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        PropertyInfo[] IModel.GetPropertyInfos() => _modelInfo._propertyInfos;

        [SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This method will not be called by child classes.")]
        PropertyInfo? IModel.GetPropertyInfo(string name)
        {
            if (_modelInfo._propertyInfoByName.TryGetValue(name, out var propertyInfo))
            {
                return propertyInfo;
            }
            return null;
        }

        public virtual long GetSchemaVersion() => _modelInfo._schemaVersion;

        public virtual string GetDatastoreNamespaceForLocalKeys()
        {
            throw new NotSupportedException("This model has a property of type 'local-key', but does not implement GetDatastoreNamespaceForLocalKeys");
        }

        public virtual Dictionary<string, ushort> GetHashKeyLengthsForGeopointFields() => _modelInfo._geoHashKeyLengths;

        public virtual IReadOnlyDictionary<string, FieldType> GetTypes() => _modelInfo._types;
    }
}
