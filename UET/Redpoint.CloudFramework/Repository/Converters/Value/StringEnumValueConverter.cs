namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Redpoint.StringEnum;
    using System;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

    internal class StringEnumValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.String;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return DynamicStringEnumValue.IsStringEnumValueType(clrType);
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            if (!DynamicStringEnumValue.TryParse(propertyClrType, propertyClrDefaultValue.ToString()!, out var parsedValue))
            {
                throw new InvalidOperationException("Invalid default defined for property: " + propertyName + " (is not a permitted value for the StringEnum)");
            }
            return parsedValue;
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var rawValue = propertyNonNullDatastoreValue.StringValue;
            if (rawValue == null)
            {
                return null;
            }
            else
            {
                if (!DynamicStringEnumValue.TryParse(propertyClrType, rawValue, out var parsedValue))
                {
                    // If we can't parse, ensure value is null.
                    parsedValue = null;
                }
                return parsedValue;
            }
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
                    StringValue = propertyClrValue.ToString() ?? string.Empty,
                    ExcludeFromIndexes = !propertyIndexed || (propertyClrValue.ToString() ?? string.Empty).Length > 700,
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
            var rawValue = propertyJsonToken.Value<string>();
            if (rawValue == null)
            {
                return null;
            }
            else
            {
                if (!DynamicStringEnumValue.TryParse(propertyClrType, rawValue, out var parsedValue))
                {
                    // If we can't parse, ensure value is null.
                    parsedValue = null;
                }
                return parsedValue;
            }
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return new JValue(propertyNonNullClrValue.ToString());
        }
    }
}
