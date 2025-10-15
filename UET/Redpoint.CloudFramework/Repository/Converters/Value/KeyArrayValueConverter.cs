namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Prefix;
    using System.Text.Json.Nodes;

    internal class KeyArrayValueConverter : BaseArrayValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public KeyArrayValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public override FieldType GetFieldType()
        {
            return FieldType.KeyArray;
        }

        public override bool IsConverterForClrType(Type propertyClrArrayType)
        {
            return propertyClrArrayType == typeof(Key[]) ||
                propertyClrArrayType == typeof(IReadOnlyList<Key>) ||
                propertyClrArrayType == typeof(List<Key>);
        }

        protected override Type GetElementType(Type propertyClrArrayType)
        {
            return typeof(Key);
        }

        protected override object ConstructClrValueFromArrayList(
            Type propertyClrArrayType,
            ArrayList arrayList)
        {
            return arrayList.Cast<Key>().ToArray();
        }

        protected override object? ConvertFromClrDefaultElementValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            object? propertyNonNullClrDefaultElementValue)
        {
            throw new InvalidOperationException("FieldType.KeyArray does not support default values. These property must be nullable and omit [Default].");
        }

        protected override object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue)
        {
            var keyValue = propertyNonNullDatastoreElementValue.KeyValue;

            if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
            {
                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
            }

            return keyValue;
        }

        protected override Value ConvertToDatastoreElementValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue,
            bool propertyIndexed)
        {
            var key = (Key)propertyNonNullClrElementValue;

            if (key.PartitionId.NamespaceId != context.ModelNamespace)
            {
                throw new InvalidOperationException("Cross-namespace data write for key property '" + propertyName + "' in array element.");
            }

            return new Value
            {
                KeyValue = key,
                ExcludeFromIndexes = !propertyIndexed,
            };
        }

        protected override object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JsonNode propertyNonNullJsonElementToken)
        {
            var idStr = JsonValueAssertions.FromStringJsonNode(propertyName, propertyNonNullJsonElementToken);
            if (idStr == null)
            {
                return null;
            }
            else
            {
                var keyValue = _globalPrefix.ParseInternal(context.ModelNamespace, idStr);

                if (keyValue != null && keyValue.PartitionId.NamespaceId != context.ModelNamespace)
                {
                    throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                }

                return keyValue;
            }
        }

        protected override JsonNode ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            var keyValue = (Key)propertyNonNullClrElementValue;

            if (keyValue.PartitionId.NamespaceId != context.ModelNamespace)
            {
                throw new InvalidOperationException("Cross-namespace data write for key property '" + propertyName + "' in array element.");
            }

            return JsonValueAssertions.ToStringJsonNode(propertyName, _globalPrefix.CreateInternal(keyValue));
        }
    }
}
