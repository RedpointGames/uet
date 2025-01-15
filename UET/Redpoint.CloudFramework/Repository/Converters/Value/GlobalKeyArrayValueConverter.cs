namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Prefix;

    internal class GlobalKeyArrayValueConverter : BaseArrayValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;

        public GlobalKeyArrayValueConverter(IGlobalPrefix globalPrefix)
        {
            _globalPrefix = globalPrefix;
        }

        public override FieldType GetFieldType()
        {
            return FieldType.GlobalKeyArray;
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
            throw new InvalidOperationException("FieldType.GlobalKeyArray does not support default values. These property must be nullable and omit [Default].");
        }

        protected override object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue)
        {
            var globalKeyValue = propertyNonNullDatastoreElementValue.KeyValue;

            if (string.IsNullOrEmpty(context.ModelNamespace))
            {
                throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace");
            }
            if (globalKeyValue != null && !string.IsNullOrEmpty(globalKeyValue.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
            }

            return globalKeyValue;
        }

        protected override Value ConvertToDatastoreElementValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue,
            bool propertyIndexed)
        {
            var key = (Key)propertyNonNullClrElementValue;

            if (context.Model.Key != null && string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Attempted to use 'global-key' field type in entity that is in the global namespace.");
            }

            if (!string.IsNullOrEmpty(key.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Non-global-namespace data write for key property '" + propertyName + "' in array element.");
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
            JToken propertyNonNullJsonElementToken)
        {
            var globalIdStr = propertyNonNullJsonElementToken.Value<string>();
            if (globalIdStr == null)
            {
                return null;
            }
            else
            {
                var globalKeyValue = _globalPrefix.ParseInternal(string.Empty, globalIdStr);

                if (string.IsNullOrEmpty(context.ModelNamespace))
                {
                    throw new InvalidOperationException("global-key properties can not be used on entities inside the global namespace.");
                }

                if (globalKeyValue != null && !string.IsNullOrEmpty(globalKeyValue.PartitionId.NamespaceId))
                {
                    throw new InvalidOperationException("Unable to load property '" + propertyName + "' from entity; cross-namespace reference detected in array element.");
                }

                return globalKeyValue;
            }
        }

        protected override JToken ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue)
        {
            var globalValue = (Key)propertyNonNullClrElementValue;

            if (string.IsNullOrEmpty(context.Model.Key.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Attempted to use 'global-key' field type in entity that is in the global namespace.");
            }

            if (!string.IsNullOrEmpty(globalValue.PartitionId.NamespaceId))
            {
                throw new InvalidOperationException("Non-global-namespace data write for key property '" + propertyName + "' in array element.");
            }

            return _globalPrefix.CreateInternal(globalValue);
        }
    }
}
