namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Type;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.JsonHelpers;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class JsonValueConverter : IValueConverter
    {
        private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new NodaTimeInstantJsonConverter()
            }
        };

        public FieldType GetFieldType()
        {
            return FieldType.Json;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return true;
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            throw new InvalidOperationException("FieldType.Json does not support default values. These property must be nullable and omit [Default].");
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "JSON fields are not supported in trimmed applications at this time.")]
        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            var rawJson = propertyNonNullDatastoreValue.StringValue;
            if (rawJson == null)
            {
                // @note: I don't think this is possible; a null value would be NullValue instead.
                return null;
            }
            else
            {
                return JsonSerializer.Deserialize(rawJson, propertyClrType, _jsonOptions);
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "JSON fields are not supported in trimmed applications at this time.")]
        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            return new Value
            {
                StringValue = JsonSerializer.Serialize(propertyClrValue, _jsonOptions),
                ExcludeFromIndexes = true /* no meaningful way to search this data in Datastore */
            };
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "JSON fields are not supported in trimmed applications at this time.")]
        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            if (propertyNonNullJsonToken == null || propertyNonNullJsonToken.GetValueKind() == JsonValueKind.Null)
            {
                throw new JsonValueWasNullException(propertyName);
            }

            if (propertyNonNullJsonToken.GetValueKind() != JsonValueKind.String)
            {
                throw new JsonValueWasIncorrectKindException(propertyName, propertyNonNullJsonToken.GetValueKind(), JsonValueKind.String);
            }

            string? rawJson = propertyNonNullJsonToken.GetValue<string>();
            if (rawJson == null)
            {
                return null;
            }
            else
            {
                return JsonSerializer.Deserialize(rawJson, propertyClrType, _jsonOptions);
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "JSON fields are not supported in trimmed applications at this time.")]
        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            if (propertyNonNullClrValue == null)
            {
                throw new RuntimeValueWasNullException(propertyName);
            }

            return JsonSerializer.Serialize(propertyNonNullClrValue, _jsonOptions);
        }
    }
}
