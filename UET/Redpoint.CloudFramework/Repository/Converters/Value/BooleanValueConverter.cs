namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class BooleanValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.Boolean;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(bool) ||
                clrType == typeof(bool?);
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            return propertyClrDefaultValue;
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return propertyNonNullDatastoreValue.BooleanValue;
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            var nullable = (bool?)propertyClrValue;
            if (!nullable.HasValue)
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
                    BooleanValue = nullable.Value,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return JsonValueAssertions.FromBooleanJsonNode(propertyName, propertyNonNullJsonToken);
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return JsonValueAssertions.ToBooleanJsonNode(propertyName, propertyNonNullClrValue);
        }
    }
}
