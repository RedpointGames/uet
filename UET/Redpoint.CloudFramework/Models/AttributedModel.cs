namespace Redpoint.CloudFramework.Models
{
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
    public class AttributedModel : Model, IGeoModel
    {
        private struct ModelInfo
        {
            public long _schemaVersion;
            public string _kind;
            public HashSet<string> _indexes;
            public Dictionary<string, FieldType> _types;
            public Dictionary<string, object> _defaultValues;
            public Dictionary<string, ushort> _geoHashKeyLengths;
        }

        private static readonly IValueConverter[] _stringEnumValueConverters = new IValueConverter[]
        {
            new StringEnumValueConverter(),
            new StringEnumArrayValueConverter(),
            new StringEnumSetValueConverter(),
        };

        private static Dictionary<Type, ModelInfo> _cachedInfo = new Dictionary<Type, ModelInfo>();
        private readonly Type _type;
        private readonly ModelInfo _modelInfo;

        [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.", Justification = "We're calling Activator.CreateInstance on the property type, where the property has a [Default] and thus must have a non-null value enforced by the C# compiler.")]
        public AttributedModel()
        {
            _type = GetType();

            if (!_cachedInfo.ContainsKey(_type))
            {
                lock (_cachedInfo)
                {
                    var kindAttribute = _type.GetCustomAttributes(typeof(IKindAttribute), false).Cast<IKindAttribute>().FirstOrDefault()
                        ?? throw new InvalidOperationException($"Missing [Kind<T>(\"...\")] attribute on {_type.FullName} class.");
                    if (kindAttribute.Type != _type)
                    {
                        throw new InvalidOperationException($"Attribute [Kind<T>(\"...\")] has T that differs from runtime type of class, which is {_type.FullName}.");
                    }

                    var typeWithRuntimeInfo = kindAttribute.Type;

                    long schemaVersion = typeWithRuntimeInfo.GetCustomAttributes(typeof(SchemaVersionAttribute), false).Cast<SchemaVersionAttribute>().FirstOrDefault()?.SchemaVersion ?? 1;
                    string kind = kindAttribute?.Kind!;
                    if (string.IsNullOrWhiteSpace(kind))
                    {
                        throw new InvalidOperationException($"Attribute [Kind<T>(\"...\")] on {_type.FullName} has an invalid value.");
                    }

                    var indexes = new HashSet<string>();
                    var types = new Dictionary<string, FieldType>();
                    var defaults = new Dictionary<string, object>();
                    var geoHashKeyLengths = new Dictionary<string, ushort>();
                    foreach (var property in typeWithRuntimeInfo.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var isIndexed = property.GetCustomAttributes(typeof(IndexedAttribute), false).Length > 0;
                        var type = property.GetCustomAttributes(typeof(TypeAttribute), false).Cast<TypeAttribute>().FirstOrDefault();
                        var @default = property.GetCustomAttributes(typeof(DefaultAttribute), false).Cast<DefaultAttribute>().FirstOrDefault();
                        var geopoint = property.GetCustomAttributes(typeof(GeopointAttribute), false).Cast<GeopointAttribute>().FirstOrDefault();
                        if (type != null)
                        {
                            if (isIndexed)
                            {
                                indexes.Add(property.Name);
                            }

                            types.Add(property.Name, type.Type);

                            if (type.Type == FieldType.Geopoint)
                            {
                                if (geopoint == null)
                                {
                                    throw new InvalidOperationException($"Missing [Geopoint(...)] attribute on Geopoint field {typeWithRuntimeInfo.FullName}.{property.Name}. This attribute is required for Geopoint fields.");
                                }
                                else
                                {
                                    geoHashKeyLengths.Add(property.Name, geopoint.HashKeyLength);
                                }
                            }

                            if (@default != null)
                            {
                                if (property.PropertyType.IsArray)
                                {
                                    // We only support empty (non-null) arrays as defaults.
#pragma warning disable IL3050 // The Array.CreateInstance will be called for an array type explicitly used by the codebase.
                                    defaults.Add(property.Name, Array.CreateInstance(property.PropertyType.GetElementType()!, 0));
#pragma warning restore IL3050
                                }
                                else
                                {
                                    defaults.Add(property.Name, @default.DefaultValue);
                                }
                            }
                            else
                            {
                                if (property.PropertyType.IsValueType &&
                                    property.PropertyType.Name != typeof(Nullable<>).Name)
                                {
                                    throw new InvalidOperationException($"Missing [Default(...)] attribute on {typeWithRuntimeInfo.FullName}.{property.Name}. Non-nullable value type properties must have the [Default] attribute. If you want to permit nulls, change this to a nullable value type instead (e.g. 'bool?' instead of 'bool').");
                                }
                            }
                        }
                    }

                    _cachedInfo[typeWithRuntimeInfo] = new ModelInfo()
                    {
                        _schemaVersion = schemaVersion,
                        _kind = kind,
                        _indexes = indexes,
                        _types = types,
                        _defaultValues = defaults,
                        _geoHashKeyLengths = geoHashKeyLengths,
                    };
                }
            }

            _modelInfo = _cachedInfo[_type];

            var conversionContext = new ClrValueConvertFromContext();
            foreach (var kv in _modelInfo._defaultValues)
            {
#pragma warning disable IL2080 // To get to this point, _type must already have been checked with kindAttribute.Type != _type
                var property = _type.GetProperty(kv.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
                var didHandle = false;
                foreach (var valueConverter in _stringEnumValueConverters)
                {
                    if (valueConverter.GetFieldType() == _modelInfo._types[property.Name] &&
                        valueConverter.IsConverterForClrType(property.PropertyType))
                    {
                        property.SetValue(
                            this,
                            valueConverter.ConvertFromClrDefaultValue(
                                conversionContext,
                                property.Name,
                                property.PropertyType,
                                kv.Value));
                        didHandle = true;
                        break;
                    }
                }
                if (!didHandle)
                {
                    property.SetValue(this, kv.Value);
                }
#pragma warning restore IL2080
            }
        }

        public sealed override HashSet<string> GetIndexes()
        {
            return _modelInfo._indexes;
        }

        public sealed override string GetKind()
        {
            return _modelInfo._kind;
        }

        public sealed override long GetSchemaVersion()
        {
            return _modelInfo._schemaVersion;
        }

        public sealed override Dictionary<string, FieldType> GetTypes()
        {
            return _modelInfo._types;
        }

        public sealed override Dictionary<string, object> GetDefaultValues()
        {
            return _modelInfo._defaultValues;
        }

        public Dictionary<string, ushort> GetHashKeyLengthsForGeopointFields()
        {
            return _modelInfo._geoHashKeyLengths;
        }
    }
}
