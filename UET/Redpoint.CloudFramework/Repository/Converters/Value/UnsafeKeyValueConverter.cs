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

    internal class UnsafeKeyValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public UnsafeKeyValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public FieldType GetFieldType()
        {
            return FieldType.UnsafeKey;
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
            throw new InvalidOperationException("FieldType.UnsafeKey does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return propertyNonNullDatastoreValue.KeyValue;
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
            var unsafeIdStr = JsonValueAssertions.FromStringJsonNode(propertyName, propertyJsonToken);
            if (unsafeIdStr == null)
            {
                return null;
            }
            else
            {
                return _globalPrefix.ParseInternal(string.Empty, unsafeIdStr);
            }
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return JsonValueAssertions.ToStringJsonNode(propertyName, _globalPrefix.CreateInternal((Key)propertyNonNullClrValue));
        }
    }
}
