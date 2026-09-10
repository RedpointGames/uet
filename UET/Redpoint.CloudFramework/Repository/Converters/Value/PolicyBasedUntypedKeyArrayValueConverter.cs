namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    internal class PolicyBasedUntypedKeyArrayValueConverter : BaseArrayValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly KeyConverterPolicy _policy;

        public PolicyBasedUntypedKeyArrayValueConverter(IGlobalPrefix globalPrefix, KeyConverterPolicy policy)
        {
            _globalPrefix = globalPrefix;
            _policy = policy;
        }

        public override FieldType GetFieldType()
        {
            return _policy switch
            {
                KeyConverterPolicy.Default => FieldType.KeyArray,
                KeyConverterPolicy.Local => throw new NotSupportedException("LocalKeyArray is not a supported field type."),
                KeyConverterPolicy.Global => FieldType.GlobalKeyArray,
                KeyConverterPolicy.Unsafe => throw new NotSupportedException("UnsafeKeyArray is not a supported field type."),
                _ => throw new NotSupportedException()
            };
        }

        public override bool IsConverterForClrType(Type propertyClrArrayType)
        {
            return GetElementType(propertyClrArrayType) != null;
        }

        protected override Type GetElementType(Type propertyClrArrayType)
        {
            if (propertyClrArrayType == typeof(UntypedKey[]) ||
                propertyClrArrayType == typeof(IReadOnlyList<UntypedKey>) ||
                propertyClrArrayType == typeof(List<UntypedKey>))
            {
                return typeof(UntypedKey);
            }

            if (propertyClrArrayType.IsArray)
            {
                var propertyClrArrayElementType = propertyClrArrayType.GetElementType()!;
                if (propertyClrArrayElementType.IsGenericType && propertyClrArrayElementType.GetGenericTypeDefinition() == typeof(Key<>))
                {
                    return propertyClrArrayElementType;
                }
            }

            if (propertyClrArrayType.IsGenericType)
            {
                var genericTypeDefinition = propertyClrArrayType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(IReadOnlyList<>) ||
                    genericTypeDefinition == typeof(List<>))
                {
                    var propertyClrArrayElementType = propertyClrArrayType.GetGenericArguments()[0];
                    if (propertyClrArrayElementType.IsGenericType && propertyClrArrayElementType.GetGenericTypeDefinition() == typeof(Key<>))
                    {
                        return propertyClrArrayElementType;
                    }
                }
            }

            // Only reached via IsConverterForClrType.
            return null!;
        }

        protected override object ConstructClrValueFromArrayList(
            Type propertyClrArrayType,
            ArrayList arrayList)
        {
            if (propertyClrArrayType == typeof(IReadOnlyList<UntypedKey>) ||
                propertyClrArrayType == typeof(UntypedKey[]))
            {
                return arrayList.Cast<UntypedKey>().ToArray();
            }

            if (propertyClrArrayType == typeof(List<UntypedKey>))
            {
                return arrayList.Cast<UntypedKey>().ToList();
            }

            var elementType = GetElementType(propertyClrArrayType);
            var referencedModel = ReferenceModelCache.GetFromKeyProperty(elementType);

            if (propertyClrArrayType.IsArray)
            {
                var array = referencedModel.ConstructDynamicTypedKeyArray(arrayList.Count);
                for (int i = 0; i < arrayList.Count; i++)
                {
                    array[i] = (UntypedKey)arrayList[i]!;
                }
                return array;
            }

            if (propertyClrArrayType.IsGenericType)
            {
                var genericTypeDefinition = propertyClrArrayType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(IReadOnlyList<>))
                {
                    var array = referencedModel.ConstructDynamicTypedKeyArray(arrayList.Count);
                    for (int i = 0; i < arrayList.Count; i++)
                    {
                        array[i] = (UntypedKey)arrayList[i]!;
                    }
                    return array;
                }

                if (genericTypeDefinition == typeof(List<>))
                {
                    var (list, addKey) = referencedModel.ConstructDynamicTypedKeyList(arrayList.Count);
                    for (int i = 0; i < arrayList.Count; i++)
                    {
                        addKey((UntypedKey)arrayList[i]!);
                    }
                    return list;
                }
            }

            throw new InvalidOperationException();
        }

        private static object? ConstructConcreteKey(Type propertyClrType, DatastoreKey? datastoreKey)
        {
            if (datastoreKey == null)
            {
                return null;
            }
            else if (propertyClrType == typeof(UntypedKey))
            {
                return ReferenceModelCache.Get(datastoreKey.Path.Last().Kind).ConvertDatastoreKeyToUntypedKey(datastoreKey);
            }
            else
            {
                return ReferenceModelCache.GetFromKeyProperty(propertyClrType).ConvertDatastoreKeyToUntypedKey(datastoreKey);
            }
        }

        private static object? ConstructConcreteKey(Type propertyClrType, UntypedKey? untypedKey)
        {
            if (untypedKey == null)
            {
                return null;
            }
            else
            {
                // UntypedKey is now abstract, so all instantiations are the correct concrete Key<T> type.
                return untypedKey;
            }
        }

        protected override object? ConvertFromClrDefaultElementValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            object? propertyNonNullClrDefaultElementValue)
        {
            throw new InvalidOperationException(
                _policy switch
                {
                    KeyConverterPolicy.Default => "FieldType.KeyArray",
                    KeyConverterPolicy.Local => "FieldType.LocalKeyArray",
                    KeyConverterPolicy.Global => "FieldType.GlobalKeyArray",
                    KeyConverterPolicy.Unsafe => "FieldType.UnsafeKeyArray",
                    _ => throw new NotSupportedException(),
                } + "does not support default values. These property must be nullable and omit [Default].");
        }

        protected override object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue)
        {
            var keyValue = propertyNonNullDatastoreElementValue.KeyValue;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                        }
                        break;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
                        }
                        if (keyValue != null && !string.IsNullOrEmpty(keyValue.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                        }
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }

            return ConstructConcreteKey(propertyClrElementType, keyValue);
        }

        protected override Value ConvertToDatastoreElementValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue,
            bool propertyIndexed)
        {
            var key = ((UntypedKey)propertyNonNullClrElementValue).__InternalDatastoreKey__;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (key.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Cross-namespace data write for key property '" + propertyName + "' in array element.");
                        }
                        break;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (context.ReferenceModel.HasKey(context.Model) &&
                            string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'global-key' field type in entity that is in the global namespace.");
                        }
                        if (!string.IsNullOrEmpty(key.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Non-global-namespace data write for key property '" + propertyName + "' in array element.");
                        }
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }

            return new Value
            {
                KeyValue = key,
                ExcludeFromIndexes = !propertyIndexed,
            };
        }

        protected override object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JsonNode propertyNonNullJsonElementToken)
        {
            var idStr = JsonValueAssertions.FromStringJsonNode(propertyName, propertyNonNullJsonElementToken);
            if (idStr == null)
            {
                return null;
            }

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        var keyValue = _globalPrefix.ParseInternal(context.ModelNamespace, idStr);

                        if (keyValue != null && keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                        }

                        return ConstructConcreteKey(propertyClrElementType, keyValue);
                    }
                case KeyConverterPolicy.Global:
                    {
                        var keyValue = _globalPrefix.ParseInternal(string.Empty, idStr);

                        if (string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace.");
                        }

                        if (keyValue != null && !string.IsNullOrEmpty(keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                        }

                        return ConstructConcreteKey(propertyClrElementType, keyValue);
                    }
                default:
                    throw new NotSupportedException();
            }
        }

        protected override JsonNode ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            var keyValue = (UntypedKey)propertyNonNullClrElementValue;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Cross-namespace data write for key property '" + propertyName + "' in array element.");
                        }
                        break;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'global-key' field type in entity that is in the global namespace.");
                        }

                        if (!string.IsNullOrEmpty(keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Non-global-namespace data write for key property '" + propertyName + "' in array element.");
                        }
                        break;
                    }
                default:
                    throw new NotSupportedException();
            }

            return JsonValueAssertions.ToStringJsonNode(propertyName, _globalPrefix.CreateInternal(keyValue, PathGenerationMode.NoShortPathComponents));
        }
    }
}
