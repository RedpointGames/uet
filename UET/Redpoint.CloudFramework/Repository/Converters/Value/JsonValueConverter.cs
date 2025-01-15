namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using System;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Newtonsoft.Json;
    using Redpoint.CloudFramework.Repository.Converters.JsonHelpers;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

    internal class JsonValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.Json;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return true;
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            throw new InvalidOperationException("FieldType.Json does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var rawJson = propertyNonNullDatastoreValue.StringValue;
            if (rawJson == null)
            {
                // @note: I don't think this is possible; a null value would be NullValue instead.
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject(rawJson, propertyClrType, new VersionedJsonConverter(), new NodaTimeInstantJsonConverter());
            }
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            return new Value
            {
                StringValue = JsonConvert.SerializeObject(propertyClrValue, new VersionedJsonConverter(), new NodaTimeInstantJsonConverter()),
                ExcludeFromIndexes = true /* no meaningful way to search this data in Datastore */
            };
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JToken propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            string? rawJson = propertyNonNullJsonToken.Value<string>();
            if (rawJson == null)
            {
                return null;
            }
            else
            {
                return JsonConvert.DeserializeObject(rawJson, propertyClrType, new
VersionedJsonConverter(), new NodaTimeInstantJsonConverter());
            }
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return JsonConvert.SerializeObject(propertyNonNullClrValue, new VersionedJsonConverter(), new NodaTimeInstantJsonConverter());
        }
    }
}
