namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;

    internal class UnsignedIntegerArrayValueConverter : BaseArrayValueConverter
    {
        public override FieldType GetFieldType()
        {
            return FieldType.UnsignedIntegerArray;
        }

        public override bool IsConverterForClrType(Type propertyClrArrayType)
        {
            return propertyClrArrayType == typeof(ulong[]) ||
                propertyClrArrayType == typeof(IReadOnlyList<ulong>) ||
                propertyClrArrayType == typeof(List<ulong>);
        }

        protected override Type GetElementType(Type propertyClrArrayType)
        {
            return typeof(ulong);
        }

        protected override object ConstructClrValueFromArrayList(
            Type propertyClrArrayType,
            ArrayList arrayList)
        {
            return arrayList.Cast<ulong>().ToArray();
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
            return unchecked((ulong)propertyNonNullDatastoreElementValue.IntegerValue);
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
                IntegerValue = unchecked((long)(ulong)propertyNonNullClrElementValue),
                ExcludeFromIndexes = !propertyIndexed
            };
        }

        protected override object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JToken propertyNonNullJsonElementToken)
        {
            return propertyNonNullJsonElementToken.Value<ulong>();
        }

        protected override JToken ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            return new JValue((ulong)propertyNonNullClrElementValue);
        }
    }
}
