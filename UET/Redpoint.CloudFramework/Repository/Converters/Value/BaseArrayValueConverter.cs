namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal abstract class BaseArrayValueConverter : IValueConverter
    {
        public abstract FieldType GetFieldType();

        public abstract bool IsConverterForClrType(Type propertyClrArrayType);

        protected abstract Type GetElementType(Type propertyClrArrayType);

        protected abstract object ConstructClrValueFromArrayList(Type propertyClrArrayType, ArrayList arrayList);

        protected abstract object? ConvertFromClrDefaultElementValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrDefaultElementValue);

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            var result = new ArrayList();
            foreach (var element in (IEnumerable)propertyClrDefaultValue)
            {
                if (element != null)
                {
                    result.Add(ConvertFromClrDefaultElementValue(
                        context,
                        propertyName,
                        GetElementType(propertyClrType),
                        element));
                }
            }
            return ConstructClrValueFromArrayList(
                propertyClrType,
                result);
        }

        protected abstract object? ConvertFromDatastoreElementValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            Value propertyNonNullDatastoreElementValue);

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var result = new ArrayList();
            foreach (var element in propertyNonNullDatastoreValue.ArrayValue.Values)
            {
                if (!element.IsNull)
                {
                    result.Add(ConvertFromDatastoreElementValue(
                        context,
                        propertyName,
                        GetElementType(propertyClrType),
                        element));
                }
            }
            return ConstructClrValueFromArrayList(
                propertyClrType,
                result);
        }

        protected abstract Value ConvertToDatastoreElementValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue,
            bool propertyIndexed);

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

            ArrayValue datastoreValue = new ArrayValue();
            foreach (var clrElementValue in (IEnumerable)propertyClrValue)
            {
                if (clrElementValue != null)
                {
                    datastoreValue.Values.Add(ConvertToDatastoreElementValue(
                        context,
                        propertyName,
                        GetElementType(propertyClrType),
                        clrElementValue,
                        propertyIndexed));
                }
            }
            return new Value
            {
                ArrayValue = datastoreValue,
                // @note: This is apparently not permitted according to the Datastore Emulator.
                // ExcludeFromIndexes = !propertyIndexed,
            };
        }

        protected abstract object? ConvertFromJsonElementToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrElementType,
            JsonNode propertyNonNullJsonElementToken);

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var result = new ArrayList();

            // @note: Guards against JSON cache tokens not being array values.
            if (propertyNonNullJsonToken.GetValueKind() == JsonValueKind.Array)
            {
                var array = propertyNonNullJsonToken.AsArray();
                foreach (var token in array)
                {
                    if (token != null && token.GetValueKind() != JsonValueKind.Null)
                    {
                        result.Add(ConvertFromJsonElementToken(
                            context,
                            propertyName,
                            GetElementType(propertyClrType),
                            token));
                    }
                }
            }

            return ConstructClrValueFromArrayList(
                propertyClrType,
                result);
        }

        protected abstract JsonNode ConvertFromJsonElementValue(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrElementType,
            object propertyNonNullClrElementValue);

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var jsonElementTokens = new JsonArray();
            foreach (var clrElementValue in (IEnumerable)propertyNonNullClrValue)
            {
                if (clrElementValue != null)
                {
                    jsonElementTokens.Add(ConvertFromJsonElementValue(
                        context,
                        propertyName,
                        GetElementType(propertyClrType),
                        clrElementValue));
                }
            }
            return jsonElementTokens;
        }
    }
}
