namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using System;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

    internal class GlobalKeyValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public GlobalKeyValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public FieldType GetFieldType()
        {
            return FieldType.GlobalKey;
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
            throw new InvalidOperationException("FieldType.GlobalKey does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var globalKeyValue = propertyNonNullDatastoreValue.KeyValue;

            if (string.IsNullOrEmpty(context.ModelNamespace))
            {
                throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
            }
            if (globalKeyValue != null && !string.IsNullOrEmpty(globalKeyValue.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
            }

            return globalKeyValue;
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
                if (context.Model.Key != null && string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
                {
                    throw new InvalidOperationException("Attempted to use 'global-key' in entity that is in the global namespace");
                }

                if (!string.IsNullOrEmpty(keyNullable.PartitionId.NamespaceId))
                {
                    throw new InvalidOperationException("Potential cross-namespace data write for key property '" + propertyName + "'");
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
            JToken propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var globalIdStr = propertyNonNullJsonToken.Value<string>();
            if (globalIdStr == null)
            {
                return null;
            }
            else
            {
                var globalKeyValue = _globalPrefix.ParseInternal(string.Empty, globalIdStr);

                if (string.IsNullOrEmpty(context.ModelNamespace))
                {
                    throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
                }
                if (globalKeyValue != null && !string.IsNullOrEmpty(globalKeyValue.PartitionId.NamespaceId))
                {
                    throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                }

                return globalKeyValue;
            }
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var globalValue = (Key)propertyNonNullClrValue;

            if (string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Attempted to use 'global-key' in entity that is in the global namespace");
            }

            if (!string.IsNullOrEmpty(globalValue.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Value for 'global-key' is not a key referencing an entity in the global namespace");
            }

            return _globalPrefix.CreateInternal(globalValue);
        }
    }
}
