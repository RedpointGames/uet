namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class StringValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.String;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(string);
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
            return propertyNonNullDatastoreValue.StringValue;
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
            else
            {
                return new Value
                {
                    StringValue = (string)propertyClrValue,
                    ExcludeFromIndexes = !propertyIndexed || ((string)propertyClrValue).Length > 700,
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
            return JsonValueAssertions.FromStringJsonNode(propertyName, propertyJsonToken);
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return JsonValueAssertions.ToStringJsonNode(propertyName, propertyNonNullClrValue);
        }
    }
}
