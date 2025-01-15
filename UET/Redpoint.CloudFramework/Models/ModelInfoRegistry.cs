namespace Redpoint.CloudFramework.Models
{
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    internal static class ModelInfoRegistry
    {
        private static Dictionary<Type, ModelInfo> _cachedInfo = [];

        private static readonly IValueConverter[] _stringEnumValueConverters =
        [
            new StringEnumValueConverter(),
            new StringEnumArrayValueConverter(),
            new StringEnumSetValueConverter(),
        ];

        public static ModelInfo InitModel<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(Model<T> model) where T : Model<T>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));
            if (typeof(T) != model.GetType() &&
                // Technically having a derived type where the derived type has no
                // additional properties is safe, but we only permit it in the test project.
                !(typeof(T).Namespace ?? string.Empty).StartsWith("Redpoint.CloudFramework.Tests", StringComparison.Ordinal))
            {
                throw new ArgumentException("The model value must have the exact same type as T.", nameof(model));
            }

            var modelInfo = GetModelInfo<T>();

            var conversionContext = new ClrValueConvertFromContext();
            foreach (var kv in modelInfo._defaultValues)
            {
                var property = typeof(T).GetProperty(kv.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
                var didHandle = false;
                foreach (var valueConverter in _stringEnumValueConverters)
                {
                    if (valueConverter.GetFieldType() == modelInfo._types[property.Name] &&
                        valueConverter.IsConverterForClrType(property.PropertyType))
                    {
                        property.SetValue(
                            model,
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
                    property.SetValue(model, kv.Value);
                }
            }

            return modelInfo;
        }

        public static ModelInfo GetModelInfo<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
        {
            if (!_cachedInfo.ContainsKey(typeof(T)))
            {
                lock (_cachedInfo)
                {
                    var kindAttribute = typeof(T).GetCustomAttributes(typeof(KindAttribute), false).Cast<KindAttribute>().FirstOrDefault()
                        ?? throw new InvalidOperationException($"Missing [Kind(\"...\")] attribute on {typeof(T).FullName} class.");

                    long schemaVersion = typeof(T).GetCustomAttributes(typeof(SchemaVersionAttribute), false).Cast<SchemaVersionAttribute>().FirstOrDefault()?.SchemaVersion ?? 1;
                    string kind = kindAttribute?.Kind!;
                    if (string.IsNullOrWhiteSpace(kind))
                    {
                        throw new InvalidOperationException($"Attribute [Kind(\"...\")] on {typeof(T).FullName} has an invalid value.");
                    }

                    var indexes = new HashSet<string>();
                    var types = new Dictionary<string, FieldType>();
                    var defaults = new Dictionary<string, object>();
                    var geoHashKeyLengths = new Dictionary<string, ushort>();
                    foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
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
                                    throw new InvalidOperationException($"Missing [Geopoint(...)] attribute on Geopoint field {typeof(T).FullName}.{property.Name}. This attribute is required for Geopoint fields.");
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
                                    defaults.Add(property.Name, Array.CreateInstance(property.PropertyType.GetElementType()!, 0));
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
                                    throw new InvalidOperationException($"Missing [Default(...)] attribute on {typeof(T).FullName}.{property.Name}. Non-nullable value type properties must have the [Default] attribute. If you want to permit nulls, change this to a nullable value type instead (e.g. 'bool?' instead of 'bool').");
                                }
                            }
                        }
                    }

                    _cachedInfo[typeof(T)] = new ModelInfo()
                    {
                        _schemaVersion = schemaVersion,
                        _kind = kind,
                        _indexes = indexes,
                        _types = types,
                        _defaultValues = defaults,
                        _geoHashKeyLengths = geoHashKeyLengths,
                        _propertyInfos = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                        _propertyInfoByName = typeof(T)
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .ToDictionary(k => k.Name, v => v),
                    };
                }
            }

            return _cachedInfo[typeof(T)];
        }
    }
}
