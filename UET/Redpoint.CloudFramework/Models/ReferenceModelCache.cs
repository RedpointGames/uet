namespace Redpoint.CloudFramework.Models
{
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    public static class ReferenceModelCache
    {
        private static Dictionary<Type, IReferenceModel> _referenceCache = [];
        private static readonly IValueConverter[] _stringEnumValueConverters =
        [
            new StringEnumValueConverter(),
            new StringEnumArrayValueConverter(),
            new StringEnumSetValueConverter(),
        ];

        /// <summary>
        /// Returns a reference model representing static metadata about a model type.
        /// </summary>
        /// <returns>The reference model.</returns>
        public static IReferenceModel<T> Get<T>() where T : class, IModel, new()
        {
            var type = typeof(T);
            if (!_referenceCache.TryGetValue(type, out var result))
            {
                // This should cause InitModel to be called.
                _ = new T();
                if (!_referenceCache.TryGetValue(type, out result))
                {
                    throw new InvalidOperationException($"Model type '{type.FullName}' is not registered with reference model cache, which should be impossible since all models implicitly call InitModel in the base constructor.");
                }
            }
            return (IReferenceModel<T>)result;
        }

        /// <summary>
        /// Returns a reference model representing static metadata about a given model.
        /// </summary>
        /// <param name="model">The model to lookup metadata for.</param>
        /// <returns>The reference model.</returns>
        public static IReferenceModel Get(IModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            var type = model.GetType();
            if (!_referenceCache.TryGetValue(type, out var result))
            {
                throw new InvalidOperationException($"Model type '{type.FullName}' is not registered with reference model cache, which should be impossible since all models implicitly call InitModel in the base constructor.");
            }
            return result;
        }

        /// <summary>
        /// Initializes a model. This registers the type with the reference model cache, and initializes all fields to the correct defaults based on the attributes that have been declared.
        /// </summary>
        /// <remarks>
        /// This method is called by the <see cref="Model{T}"/> base constructor. You don't need to call it from your own code.
        /// </remarks>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="model">The model to initialize.</param>
        /// <exception cref="ArgumentException">Thrown if the model is an incorrect inherited type.</exception>
        internal static void InitModel<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(Model<T> model) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));
            if (typeof(T) != model.GetType())
            {
                throw new ArgumentException("The model value must have the exact same type as T.", nameof(model));
            }

            var modelInfo = GetReferenceModelInfo<T>();

            var conversionContext = new ClrValueConvertFromContext();
            foreach (var kv in modelInfo.DefaultValues)
            {
                var property = modelInfo.GetPropertyInfo(kv.Key)!;
                var didHandle = false;
                foreach (var valueConverter in _stringEnumValueConverters)
                {
                    if (valueConverter.GetFieldType() == modelInfo.Types[property.Name] &&
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
        }

        /// <summary>
        /// Get or initialize the cached model info for a given model type.
        /// </summary>
        private static ReferenceModel<T> GetReferenceModelInfo<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>() where T : class, IModel, new()
        {
            if (!_referenceCache.ContainsKey(typeof(T)))
            {
                lock (_referenceCache)
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
                    var isGeoModel = false;
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
                                    isGeoModel = true;
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

                    _referenceCache[typeof(T)] = new ReferenceModel<T>(new ReferenceModelInfo()
                    {
                        CSharpTypeName = typeof(T).Name,
                        SchemaVersion = schemaVersion,
                        Kind = kind,
                        Indexes = indexes,
                        Types = types,
                        DefaultValues = defaults,
                        GeoHashKeyLengths = geoHashKeyLengths,
                        IsGeoModel = isGeoModel,
                        PropertyInfos = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                        PropertyInfoByName = typeof(T)
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .ToDictionary(k => k.Name, v => v),
                    });
                }
            }

            return (ReferenceModel<T>)_referenceCache[typeof(T)];
        }

        private sealed class ReferenceModelInfo
        {
            public required string CSharpTypeName;
            public required long SchemaVersion;
            public required string Kind;
            public required HashSet<string> Indexes;
            public required Dictionary<string, FieldType> Types;
            public required Dictionary<string, object> DefaultValues;
            public required Dictionary<string, ushort> GeoHashKeyLengths;
            public required bool IsGeoModel;
            public required PropertyInfo[] PropertyInfos;
            public required Dictionary<string, PropertyInfo> PropertyInfoByName;
        }

        private sealed class ReferenceModel<T> : IReferenceModel<T> where T : class, IModel, new()
        {
            private readonly ReferenceModelInfo _modelInfo;

            public ReferenceModel(ReferenceModelInfo modelInfo)
            {
                _modelInfo = modelInfo;
            }

            public string CSharpTypeName
            {
                get
                {
                    return _modelInfo.CSharpTypeName;
                }
            }

            public string Kind
            {
                get
                {
                    return _modelInfo.Kind;
                }
            }

            public HashSet<string> Indexes
            {
                get
                {
                    return _modelInfo.Indexes;
                }
            }

            public IReadOnlyDictionary<string, FieldType> Types
            {
                get
                {
                    return _modelInfo.Types;
                }
            }

            public Dictionary<string, object> DefaultValues
            {
                get
                {
                    return _modelInfo.DefaultValues;
                }
            }

            public IReadOnlyList<PropertyInfo> PropertyInfos
            {
                get
                {
                    return _modelInfo.PropertyInfos;
                }
            }

            public PropertyInfo? GetPropertyInfo(string name)
            {
                if (_modelInfo.PropertyInfoByName.TryGetValue(name, out var propertyInfo))
                {
                    return propertyInfo;
                }

                return null;
            }

            public long SchemaVersion
            {
                get
                {
                    return _modelInfo.SchemaVersion;
                }
            }

            public Dictionary<string, ushort> HashKeyLengthsForGeopointFields
            {
                get
                {
                    return _modelInfo.GeoHashKeyLengths;
                }
            }

            public bool IsGeoModel
            {
                get
                {
                    return _modelInfo.IsGeoModel;
                }
            }

            public T ConstructNewModel()
            {
                return new T();
            }
        }
    }
}
