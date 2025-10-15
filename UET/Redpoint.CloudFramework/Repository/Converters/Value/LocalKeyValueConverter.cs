namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class LocalKeyValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public LocalKeyValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public FieldType GetFieldType()
        {
            return FieldType.LocalKey;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(Key);
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            throw new InvalidOperationException("FieldType.LocalKey does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var localKeyValue = propertyNonNullDatastoreValue.KeyValue;

            if (!string.IsNullOrEmpty(context.ModelNamespace))
            {
                throw new InvalidOperationException("local-key properties can not be used on entities outside the global namespace");
            }

            // We can't assign yet because we need to check that the loaded namespace value is 
            // valid for GetDatastoreNamespaceForLocalKeys, but we can't use that method
            // until everything else has been loaded.
            addConvertFromDelayedLoad((@localNamespace) =>
            {
                if (localKeyValue != null && localKeyValue.PartitionId.NamespaceId != localNamespace)
                {
                    throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                }

                return localKeyValue;
            });

            return null;
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            var keyNullable = (Key?)propertyClrValue;
            if (keyNullable == null)
            {
                return new Value
                {
                    NullValue = NullValue.NullValue,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }
            else
            {
                if (context.Model.Key != null && !string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
                {
                    throw new InvalidOperationException("Attempted to use 'local-key' in entity that is not in the global namespace");
                }

                if (keyNullable.PartitionId.NamespaceId != context.Model.GetDatastoreNamespaceForLocalKeys())
                {
                    throw new InvalidOperationException(
                        "Potential cross-namespace data write for key property '" + propertyName +
                        "' (got '" + keyNullable.PartitionId.NamespaceId + "', expected '" + context.Model.GetDatastoreNamespaceForLocalKeys() + "')"
                    );
                }

                return new Value
                {
                    KeyValue = keyNullable,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var localIdStr = JsonValueAssertions.FromStringJsonNode(propertyName, propertyJsonToken);
            if (localIdStr == null)
            {
                return null;
            }
            else
            {
                var localKeyValue = _globalPrefix.ParseInternal(context.ModelNamespace, localIdStr);

                if (!string.IsNullOrEmpty(context.ModelNamespace))
                {
                    throw new InvalidOperationException("local-key properties can not be used on entities outside the global namespace");
                }

                // We can't call GetDatastoreNamespaceForLocalKeys on the model until we've set all the
                // other properties, since determining the Datastore namespace for local keys might 
                // rely on other properties.
                addConvertFromDelayedLoad((localNamespace) =>
                {
                    if (localKeyValue != null && localKeyValue.PartitionId.NamespaceId != localNamespace)
                    {
                        throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                    }

                    return localKeyValue;
                });

                return null;
            }
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var localValue = (Key)propertyNonNullClrValue;

            if (!string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Attempted to use 'local-key' in entity that is not in the global namespace");
            }

            if (localValue.PartitionId.NamespaceId != context.Model.GetDatastoreNamespaceForLocalKeys())
            {
                throw new InvalidOperationException("Value for 'local-key' is not a key referencing an entity in the expected non-global namespace");
            }

            return JsonValueAssertions.ToStringJsonNode(propertyName, _globalPrefix.CreateInternal(localValue));
        }
    }
}
