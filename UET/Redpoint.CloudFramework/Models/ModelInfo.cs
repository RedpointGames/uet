namespace Redpoint.CloudFramework.Models
{
    using System.Collections.Generic;
    using System.Reflection;

    internal sealed class ModelInfo
    {
        public required long _schemaVersion;
        public required string _kind;
        public required HashSet<string> _indexes;
        public required Dictionary<string, FieldType> _types;
        public required Dictionary<string, object> _defaultValues;
        public required Dictionary<string, ushort> _geoHashKeyLengths;
        public required PropertyInfo[] _propertyInfos;
        public required Dictionary<string, PropertyInfo> _propertyInfoByName;
    }
}
