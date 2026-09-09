namespace Redpoint.CloudFramework.Repository.ReferenceCache
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Geographic;
    using System.Collections.Generic;
    using System.Reflection;

    internal sealed class ReferenceModel<T> : IReferenceModel<T> where T : class, IModel, new()
    {
        private readonly T _reference;

        public ReferenceModel(T model)
        {
            _reference = model;
        }

        public string CSharpTypeName
        {
            get
            {
                return _reference.GetType().Name;
            }
        }

        public string Kind
        {
            get
            {
                return _reference.GetKind();
            }
        }

        public HashSet<string> Indexes
        {
            get
            {
                return _reference.GetIndexes();
            }
        }

        public IReadOnlyDictionary<string, FieldType> Types
        {
            get
            {
                return _reference.GetTypes();
            }
        }

        public Dictionary<string, object>? DefaultValues
        {
            get
            {
                return _reference.GetDefaultValues();
            }
        }

        public IReadOnlyList<PropertyInfo> PropertyInfos
        {
            get
            {
                return _reference.GetPropertyInfos();
            }
        }

        public PropertyInfo? GetPropertyInfo(string name)
        {
            return _reference.GetPropertyInfo(name);
        }

        public long SchemaVersion
        {
            get
            {
                return _reference.GetSchemaVersion();
            }
        }

        public Dictionary<string, ushort> HashKeyLengthsForGeopointFields
        {
            get
            {
                return ((IGeoModel)(_reference)).GetHashKeyLengthsForGeopointFields();
            }
        }
    }
}
