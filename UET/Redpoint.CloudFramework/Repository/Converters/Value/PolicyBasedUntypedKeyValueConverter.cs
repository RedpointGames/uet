namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using System;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using DatastoreKey = Google.Cloud.Datastore.V1.Key;

    internal class PolicyBasedUntypedKeyValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly KeyConverterPolicy _policy;

        public PolicyBasedUntypedKeyValueConverter(IGlobalPrefix globalPrefix, KeyConverterPolicy policy)
        {
            _globalPrefix = globalPrefix;
            _policy = policy;
        }

        public FieldType GetFieldType()
        {
            return _policy switch
            {
                KeyConverterPolicy.Default => FieldType.Key,
                KeyConverterPolicy.Local => FieldType.LocalKey,
                KeyConverterPolicy.Global => FieldType.GlobalKey,
                KeyConverterPolicy.Unsafe => FieldType.UnsafeKey,
                _ => throw new NotSupportedException()
            };
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(UntypedKey) ||
                (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Key<>));
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            throw new InvalidOperationException(
                _policy switch
                {
                    KeyConverterPolicy.Default => "FieldType.Key",
                    KeyConverterPolicy.Local => "FieldType.LocalKey",
                    KeyConverterPolicy.Global => "FieldType.GlobalKey",
                    KeyConverterPolicy.Unsafe => "FieldType.UnsafeKey",
                    _ => throw new NotSupportedException(),
                } + "does not support default values. These property must be nullable and omit [Default].");
        }

        private static object? ConstructConcreteKey(Type propertyClrType, DatastoreKey? datastoreKey)
        {
            if (datastoreKey == null)
            {
                return null;
            }
            else if (propertyClrType == typeof(UntypedKey))
            {
                return new UntypedKey(datastoreKey);
            }
            else
            {
                return ReferenceModelCache.GetFromKeyProperty(propertyClrType).ConvertDatastoreKeyToDynamicTypedKey(datastoreKey);
            }
        }

        private static object? ConstructConcreteKey(Type propertyClrType, UntypedKey? untypedKey)
        {
            if (untypedKey == null)
            {
                return null;
            }
            else if (propertyClrType == typeof(UntypedKey))
            {
                return untypedKey;
            }
            else
            {
                // The ParseLimited calls don't construct the concrete Key<T> type, so we need to change the wrapping type here.
                return ReferenceModelCache.GetFromKeyProperty(propertyClrType).ConvertDatastoreKeyToDynamicTypedKey(untypedKey.__InternalDatastoreKey__);
            }
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var keyValue = propertyNonNullDatastoreValue.KeyValue;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                        }

                        break;
                    }
                case KeyConverterPolicy.Local:
                    {
                        if (!string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("local-key properties can not be used on entities outside the global namespace");
                        }

                        // We can't assign yet because we need to check that the loaded namespace value is 
                        // valid for GetDatastoreNamespaceForLocalKeys, but we can't use that method
                        // until everything else has been loaded.
                        addConvertFromDelayedLoad((@localNamespace) =>
                        {
                            if (keyValue != null && keyValue.PartitionId.NamespaceId != localNamespace)
                            {
                                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                            }

                            return ConstructConcreteKey(propertyClrType, keyValue);
                        });

                        return null;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
                        }
                        if (keyValue != null && !string.IsNullOrEmpty(keyValue.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                        }

                        break;
                    }
                case KeyConverterPolicy.Unsafe:
                    break;
                default:
                    throw new NotSupportedException();
            }

            return ConstructConcreteKey(propertyClrType, keyValue);
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            if (propertyClrValue == null)
            {
                return new Value
                {
                    NullValue = NullValue.NullValue,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }

            var key = ((UntypedKey)propertyClrValue).__InternalDatastoreKey__;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (key.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Potential cross-namespace data write for key property '" + propertyName + "'");
                        }

                        break;
                    }
                case KeyConverterPolicy.Local:
                    {
                        if (context.ReferenceModel.HasKey(context.Model) &&
                            !string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'local-key' in entity that is not in the global namespace");
                        }

                        if (key.PartitionId.NamespaceId != context.Model.GetDatastoreNamespaceForLocalKeys())
                        {
                            throw new InvalidOperationException(
                                "Potential cross-namespace data write for key property '" + propertyName +
                                "' (got '" + key.PartitionId.NamespaceId + "', expected '" + context.Model.GetDatastoreNamespaceForLocalKeys() + "')"
                            );
                        }

                        break;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (context.ReferenceModel.HasKey(context.Model) && string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'global-key' in entity that is in the global namespace");
                        }

                        if (!string.IsNullOrEmpty(key.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Potential cross-namespace data write for key property '" + propertyName + "'");
                        }

                        break;
                    }
                case KeyConverterPolicy.Unsafe:
                    break;
                default:
                    throw new NotSupportedException();
            }

            return new Value
            {
                KeyValue = key,
                ExcludeFromIndexes = !propertyIndexed,
            };
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var idStr = JsonValueAssertions.FromStringJsonNode(propertyName, propertyNonNullJsonToken);
            if (idStr == null)
            {
                return null;
            }

            UntypedKey keyValue;
            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    keyValue = _globalPrefix.ParseInternal(context.ModelNamespace, idStr);
                    break;
                case KeyConverterPolicy.Local:
                    keyValue = _globalPrefix.ParseInternal(context.ModelNamespace, idStr);
                    break;
                case KeyConverterPolicy.Global:
                    keyValue = _globalPrefix.ParseInternal(string.Empty, idStr);
                    break;
                case KeyConverterPolicy.Unsafe:
                    keyValue = _globalPrefix.ParseInternal(string.Empty, idStr);
                    break;
                default:
                    throw new NotSupportedException();
            }

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (keyValue != null && keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                        }

                        break;
                    }
                case KeyConverterPolicy.Local:
                    {
                        if (!string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("local-key properties can not be used on entities outside the global namespace");
                        }

                        // We can't call GetDatastoreNamespaceForLocalKeys on the model until we've set all the
                        // other properties, since determining the Datastore namespace for local keys might 
                        // rely on other properties.
                        addConvertFromDelayedLoad((localNamespace) =>
                        {
                            if (keyValue != null && keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != localNamespace)
                            {
                                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                            }

                            return ConstructConcreteKey(propertyClrType, keyValue);
                        });

                        return null;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (string.IsNullOrEmpty(context.ModelNamespace))
                        {
                            throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
                        }
                        if (keyValue != null && !string.IsNullOrEmpty(keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                        }

                        break;
                    }
                case KeyConverterPolicy.Unsafe:
                    break;
            }

            return ConstructConcreteKey(propertyClrType, keyValue);
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var keyValue = (UntypedKey)propertyNonNullClrValue;

            switch (_policy)
            {
                case KeyConverterPolicy.Default:
                    {
                        if (keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != context.ModelNamespace)
                        {
                            throw new InvalidOperationException("Attempted to store cross-namespace key reference in 'key' property");
                        }

                        break;
                    }
                case KeyConverterPolicy.Local:
                    {
                        if (!string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'local-key' in entity that is not in the global namespace");
                        }

                        if (keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId != context.Model.GetDatastoreNamespaceForLocalKeys())
                        {
                            throw new InvalidOperationException("Value for 'local-key' is not a key referencing an entity in the expected non-global namespace");
                        }

                        break;
                    }
                case KeyConverterPolicy.Global:
                    {
                        if (string.IsNullOrEmpty(context.ReferenceModel.GetDatastoreKey(context.Model)!.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Attempted to use 'global-key' in entity that is in the global namespace");
                        }

                        if (!string.IsNullOrEmpty(keyValue.__InternalDatastoreKey__.PartitionId.NamespaceId))
                        {
                            throw new InvalidOperationException("Value for 'global-key' is not a key referencing an entity in the global namespace");
                        }

                        break;
                    }
                case KeyConverterPolicy.Unsafe:
                    break;
                default:
                    throw new NotSupportedException();
            }

            return JsonValueAssertions.ToStringJsonNode(propertyName, _globalPrefix.CreateInternal(keyValue, PathGenerationMode.NoShortPathComponents));
        }
    }
}
