namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class DoubleValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.Double;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(double) ||
                clrType == typeof(double?);
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
            return propertyNonNullDatastoreValue.DoubleValue;
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            var nullable = (double?)propertyClrValue;
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
                    DoubleValue = nullable.Value,
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
            return JsonValueAssertions.FromDoubleJsonNode(propertyName, propertyNonNullJsonToken);
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return JsonValueAssertions.ToDoubleJsonNode(propertyName, propertyNonNullClrValue);
        }
    }
}
