namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;
    using Redpoint.StringEnum;
    using System.Text.Json.Nodes;

    internal class StringEnumArrayValueConverter : BaseArrayValueConverter
    {
        public override FieldType GetFieldType()
        {
            return FieldType.StringArray;
        }

        public override bool IsConverterForClrType(Type propertyClrArrayType)
        {
            if (propertyClrArrayType.IsArray)
            {
                return DynamicStringEnumValue.IsStringEnumValueType(propertyClrArrayType.GetElementType()!);
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return DynamicStringEnumValue.IsStringEnumValueType(propertyClrArrayType.GetGenericArguments()[0]);
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return DynamicStringEnumValue.IsStringEnumValueType(propertyClrArrayType.GetGenericArguments()[0]);
            }
            return false;
        }

        protected override Type GetElementType(Type propertyClrArrayType)
        {
            if (propertyClrArrayType.IsArray)
            {
                return propertyClrArrayType.GetElementType()!;
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return propertyClrArrayType.GetGenericArguments()[0];
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return propertyClrArrayType.GetGenericArguments()[0];
            }
            else
            {
                throw new NotSupportedException($"Can't support {propertyClrArrayType.FullName} in StringEnumArrayValueConverter.GetElementType");
            }
        }

        protected override object ConstructClrValueFromArrayList(
            Type propertyClrArrayType,
            ArrayList arrayList)
        {
            if (propertyClrArrayType.IsArray)
            {
                return DynamicStringEnumValue.ConstructArrayFromValues(
                    propertyClrArrayType.GetGenericArguments()[0],
                    arrayList);
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return DynamicStringEnumValue.ConstructListFromValues(
                    propertyClrArrayType.GetGenericArguments()[0],
                    arrayList);
            }
            else if (propertyClrArrayType.IsGenericType &&
                propertyClrArrayType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return DynamicStringEnumValue.ConstructListFromValues(
                    propertyClrArrayType.GetGenericArguments()[0],
                    arrayList);
            }
            else
            {
                throw new NotSupportedException($"Can't support {propertyClrArrayType.FullName} in StringEnumArrayValueConverter.ConstructClrValueFromArrayList");
            }
        }

        protected override object? ConvertFromClrDefaultElementValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrDefaultElementValue)
        {
            if (!DynamicStringEnumValue.TryParse(propertyClrElementType, propertyNonNullClrDefaultElementValue.ToString()!, out var parsedValue))
            {
                throw new InvalidOperationException("Invalid default defined for property: " + propertyName + " (is not a permitted value for the StringEnum)");
            }
            return parsedValue;
        }

        protected override object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue)
        {
            var rawValue = propertyNonNullDatastoreElementValue.StringValue;
            if (rawValue == null)
            {
                return null;
            }
            else
            {
                if (!DynamicStringEnumValue.TryParse(propertyClrElementType, rawValue, out var parsedValue))
                {
                    // If we can't parse, ensure value is null.
                    parsedValue = null;
                }
                return parsedValue;
            }
        }

        protected override Value ConvertToDatastoreElementValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue,
            bool propertyIndexed)
        {
            return new Value
            {
                StringValue = propertyNonNullClrElementValue.ToString() ?? string.Empty,
                ExcludeFromIndexes = !propertyIndexed || (propertyNonNullClrElementValue.ToString() ?? string.Empty).Length > 700,
            };
        }

        protected override object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JsonNode propertyNonNullJsonElementToken)
        {
            var rawValue = JsonValueAssertions.FromStringJsonNode(propertyName, propertyNonNullJsonElementToken);
            if (rawValue == null)
            {
                return null;
            }
            else
            {
                if (!DynamicStringEnumValue.TryParse(propertyClrElementType, rawValue, out var parsedValue))
                {
                    // If we can't parse, ensure value is null.
                    parsedValue = null;
                }
                return parsedValue;
            }
        }

        protected override JsonNode ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            return JsonValueAssertions.ToStringJsonNode(propertyName, propertyNonNullClrElementValue.ToString());
        }
    }
}
