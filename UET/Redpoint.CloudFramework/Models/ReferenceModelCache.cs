namespace Redpoint.CloudFramework.Models
{
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using static Redpoint.CloudFramework.Models.ReferenceModelCache;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    public static class ReferenceModelCache
    {
        private static Dictionary<Type, IReferenceModel> _referenceCache = [];
        private static Dictionary<Type, IReferenceModel> _referenceByKeyCache = [];
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

        internal static IReferenceModel GetFromKeyProperty(Type keyOfPropertyType)
        {
            ArgumentNullException.ThrowIfNull(keyOfPropertyType);
            if (!_referenceByKeyCache.TryGetValue(keyOfPropertyType, out var result))
            {
                throw new InvalidOperationException($"Key type '{keyOfPropertyType.FullName}' is not registered with reference by key model cache, which should be impossible since all models implicitly call InitModel in the base constructor and iterate through all of their properties that might use Key<T> in deserialization.");
            }
            return result;
        }

        internal delegate DatastoreKey? GetDatastoreKey<T>(Model<T> model) where T : class, IModel, new();
        internal delegate void SetDatastoreKey<T>(Model<T> model, DatastoreKey? key) where T : class, IModel, new();
        internal delegate Key<T>? GetTypedKey<T>(Model<T> model) where T : class, IModel, new();

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
            [DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Model<T> model,
            GetDatastoreKey<T> getDatastoreKey,
            SetDatastoreKey<T> setDatastoreKey,
            GetTypedKey<T> getTypedKey) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));
            if (typeof(T) != model.GetType())
            {
                throw new ArgumentException("The model value must have the exact same type as T.", nameof(model));
            }

            var modelInfo = GetReferenceModelInfo<T>(
                getDatastoreKey,
                setDatastoreKey,
                getTypedKey);

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

        private readonly record struct ReferencedModelTypeContainer([property: DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] Type type);

        /// <summary>
        /// Get or initialize the cached model info for a given model type.
        /// </summary>
        private static ReferenceModel<T> GetReferenceModelInfo<
            [DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            GetDatastoreKey<T> getDatastoreKey,
            SetDatastoreKey<T> setDatastoreKey,
            GetTypedKey<T> getTypedKey) where T : class, IModel, new()
        {
            if (!_referenceCache.ContainsKey(typeof(T)))
            {
                var modelTypesReferencedFromKeyFields = new List<ReferencedModelTypeContainer>();

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

                            // Check for model types that are initially referenced via a Key<T> property type. We need to make sure these are also registered in the reference model cache, in case the first reference to the model is via a key load. If we don't do this, the model type won't be in the reference model cache, and we won't be able to instantiate Key<T> for the underlying datastore key.
                            var keyOfType = GetKeyOfTypeForProperty(property.PropertyType);
                            if (keyOfType != null &&
                                keyOfType.IsClass &&
                                keyOfType.IsAssignableTo(typeof(IModel)))
                            {
                                var concreteType = typeof(Model<>).MakeGenericType(keyOfType);
                                if (keyOfType.IsAssignableTo(concreteType))
                                {
                                    // Concrete type is the base Model<T> type, while keyOfType is the actual user defined model type.
                                    if (!_referenceCache.ContainsKey(keyOfType))
                                    {
                                        modelTypesReferencedFromKeyFields.Add(new ReferencedModelTypeContainer(keyOfType));
                                    }
                                }
                            }
                        }
                    }

                    var referenceModel = new ReferenceModel<T>(
                        new ReferenceModelInfo()
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
                        },
                        getDatastoreKey,
                        setDatastoreKey,
                        getTypedKey);
                    _referenceCache[typeof(T)] = referenceModel;
                    _referenceByKeyCache[typeof(Key<T>)] = referenceModel;
                }

                foreach (var referencedType in modelTypesReferencedFromKeyFields)
                {
                    // This will cause InitModel to run, registering the type if necessary.
                    _ = Activator.CreateInstance(referencedType.type);
                }
            }

            return (ReferenceModel<T>)_referenceCache[typeof(T)];
        }

        [return: DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)]
        [UnconditionalSuppressMessage("Trimming", "IL2063:The return value of method has a DynamicallyAccessedMembersAttribute, but the value returned from the method can not be statically analyzed.", Justification = "The DynamicallyAccessedMembers attribute here matches the one on Key<>.")]
        private static Type? GetKeyOfTypeForProperty(Type propertyType)
        {
            if (propertyType.IsGenericType &&
                propertyType.GetGenericTypeDefinition() == typeof(Key<>))
            {
                return propertyType.GetGenericArguments()[0];
            }

            if (propertyType.IsArray)
            {
                return GetKeyOfTypeForProperty(propertyType.GetElementType()!);
            }

            if (propertyType.IsGenericType &&
                propertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return GetKeyOfTypeForProperty(propertyType.GetGenericArguments()[0]);
            }

            if (propertyType.IsGenericType &&
                propertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return GetKeyOfTypeForProperty(propertyType.GetGenericArguments()[0]);
            }

            return null;
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

        private sealed class ReferenceModel<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T> : IReferenceModel<T> where T : class, IModel, new()
        {
            private readonly ReferenceModelInfo _modelInfo;
            private readonly GetDatastoreKey<T> _getDatastoreKey;
            private readonly SetDatastoreKey<T> _setDatastoreKey;
            private readonly GetTypedKey<T> _getTypedKey;

            public ReferenceModel(
                ReferenceModelInfo modelInfo,
                GetDatastoreKey<T> getDatastoreKey,
                SetDatastoreKey<T> setDatastoreKey,
                GetTypedKey<T> getTypedKey)
            {
                _modelInfo = modelInfo;
                _getDatastoreKey = getDatastoreKey;
                _setDatastoreKey = setDatastoreKey;
                _getTypedKey = getTypedKey;
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

            public bool HasKey(IModel model)
            {
                return _getTypedKey((Model<T>)model) != null;
            }

            public void SetDatastoreKey(IModel model, DatastoreKey? key)
            {
                _setDatastoreKey((Model<T>)model, key);
            }

            public DatastoreKey? GetDatastoreKey(IModel model)
            {
                return _getDatastoreKey((Model<T>)model);
            }

            public UntypedKey? GetUntypedKey(IModel model)
            {
                return _getTypedKey((Model<T>)model);
            }

            public Key<T>? GetTypedKey(IModel model)
            {
                return _getTypedKey((Model<T>)model);
            }

            [return: NotNullIfNotNull(nameof(key))]
            public UntypedKey? ConvertDatastoreKeyToUntypedKey(DatastoreKey? key)
            {
                if (key == null)
                {
                    return null;
                }

                return new UntypedKey(key);
            }

            [return: NotNullIfNotNull(nameof(key))]
            public object? ConvertDatastoreKeyToDynamicTypedKey(DatastoreKey? key)
            {
                if (key == null)
                {
                    return null;
                }

                return new Key<T>(key);
            }

            [return: NotNullIfNotNull(nameof(key))]
            public Key<T>? ConvertDatastoreKeyToTypedKey(DatastoreKey? key)
            {
                if (key == null)
                {
                    return null;
                }

                return new Key<T>(key);
            }

            public UntypedKey[] ConstructDynamicTypedKeyArray(int length)
            {
                return new Key<T>[length];
            }

            public (object list, Action<UntypedKey> addEntry) ConstructDynamicTypedKeyList(int length)
            {
                var list = new List<Key<T>>();
                return (list, key => list.Add((Key<T>)key));
            }
        }
    }
}
