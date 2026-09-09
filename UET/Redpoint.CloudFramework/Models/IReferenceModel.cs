namespace Redpoint.CloudFramework.Models
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    public interface IReferenceModel
    {
        string CSharpTypeName { get; }
        string Kind { get; }
        HashSet<string> Indexes { get; }
        IReadOnlyDictionary<string, FieldType> Types { get; }
        Dictionary<string, object>? DefaultValues { get; }
        IReadOnlyList<PropertyInfo> PropertyInfos { get; }
        PropertyInfo? GetPropertyInfo(string name);
        long SchemaVersion { get; }
        Dictionary<string, ushort> HashKeyLengthsForGeopointFields { get; }
        bool IsGeoModel { get; }
        void SetDatastoreKey(IModel model, DatastoreKey? key);
        bool HasKey(IModel model);
        DatastoreKey? GetDatastoreKey(IModel model);
        UntypedKey? GetUntypedKey(IModel model);
        [return: NotNullIfNotNull(nameof(key))]
        UntypedKey? ConvertDatastoreKeyToUntypedKey(DatastoreKey? key);
        [return: NotNullIfNotNull(nameof(key))]
        object? ConvertDatastoreKeyToDynamicTypedKey(DatastoreKey? key);
        UntypedKey[] ConstructDynamicTypedKeyArray(int length);
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This is a factory method.")]
        (object list, Action<UntypedKey> addEntry) ConstructDynamicTypedKeyList(int length);
    }

    public interface IReferenceModel<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T> : IReferenceModel where T : class, IModel, new()
    {
        T ConstructNewModel();
        Key<T>? GetTypedKey(IModel model);
        [return: NotNullIfNotNull(nameof(key))]
        Key<T>? ConvertDatastoreKeyToTypedKey(DatastoreKey? key);
    }
}
