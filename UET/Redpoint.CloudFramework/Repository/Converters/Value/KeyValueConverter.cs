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

    internal class KeyValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public KeyValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public FieldType GetFieldType()
        {
            return FieldType.Key;
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
            throw new InvalidOperationException("FieldType.Key does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var keyValue = propertyNonNullDatastoreValue.KeyValue;

            if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
            {
                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
            }

            return keyValue;
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
                if (keyNullable.PartitionId.NamespaceId != context.ModelNamespace)
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
            var idStr = propertyNonNullJsonToken.Value<string>();
            if (idStr == null)
            {
                return null;
            }
            else
            {
                var keyValue = _globalPrefix.ParseInternal(context.ModelNamespace, idStr);

                if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
                {
                    throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected");
                }

                return keyValue;
            }
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var keyValue = (Key)propertyNonNullClrValue;

            if (keyValue.PartitionId.NamespaceId != context.ModelNamespace)
            {
                throw new InvalidOperationException("Attempted to store cross-namespace key reference in 'key' property");
            }

            return _globalPrefix.CreateInternal(keyValue);
        }
    }
}
