namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

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
            JToken propertyJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return propertyJsonToken.Value<string>();
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return new JValue((string)propertyNonNullClrValue);
        }
    }
}
