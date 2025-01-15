namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;

    internal class StringArrayValueConverter : BaseArrayValueConverter
    {
        public override FieldType GetFieldType()
        {
            return FieldType.StringArray;
        }

        public override bool IsConverterForClrType(Type propertyClrArrayType)
        {
            return propertyClrArrayType == typeof(string[]) ||
                propertyClrArrayType == typeof(IReadOnlyList<string>) ||
                propertyClrArrayType == typeof(List<string>);
        }

        protected override Type GetElementType(Type propertyClrArrayType)
        {
            return typeof(string);
        }

        protected override object ConstructClrValueFromArrayList(
            Type propertyClrArrayType,
            ArrayList arrayList)
        {
            return arrayList.Cast<string>().ToArray();
        }

        protected override object? ConvertFromClrDefaultElementValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrDefaultElementValue)
        {
            return propertyNonNullClrDefaultElementValue;
        }

        protected override object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue)
        {
            return propertyNonNullDatastoreElementValue.StringValue;
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
                StringValue = (string)propertyNonNullClrElementValue,
                ExcludeFromIndexes = !propertyIndexed
            };
        }

        protected override object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JToken propertyNonNullJsonElementToken)
        {
            return propertyNonNullJsonElementToken.Value<string>();
        }

        protected override JToken ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            return new JValue(propertyNonNullClrElementValue);
        }
    }
}
