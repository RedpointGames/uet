namespace Redpoint.CloudFramework.Repository.ReferenceCache
{
    using Redpoint.CloudFramework.Models;
    using System.Collections.Generic;
    using System.Reflection;

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
    }

    public interface IReferenceModel<T> : IReferenceModel
    {
    }
}
