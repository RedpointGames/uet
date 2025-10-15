namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    internal static class JsonValueAssertions
    {
        [return: NotNull]
        private static T FromJsonNode<T>(string propertyName, JsonNode value, JsonValueKind expectedKind)
        {
            if (value == null || value.GetValueKind() == JsonValueKind.Null)
            {
                throw new JsonValueWasNullException(propertyName);
            }

            if (value.GetValueKind() != expectedKind)
            {
                throw new JsonValueWasIncorrectKindException(propertyName, value.GetValueKind(), expectedKind);
            }

            var result = value.GetValue<T>();
            if (result == null)
            {
                throw new JsonValueWasNullException(propertyName);
            }

            return result;
        }

        private static void CheckIncomingType<T>(string propertyName, [NotNull] object? value)
        {
            if (value == null)
            {
                throw new RuntimeValueWasNullException(propertyName);
            }

            if (value is not T)
            {
                throw new RuntimeValueWasIncorrectTypeException(propertyName, value, typeof(T));
            }
        }

        public static JsonNode ToStringJsonNode(string propertyName, [NotNull] object? value)
        {
            CheckIncomingType<string>(propertyName, value);
            return JsonValue.Create((string)value);
        }

        [return: NotNull]
        public static string FromStringJsonNode(string propertyName, JsonNode value)
        {
            return FromJsonNode<string>(propertyName, value, JsonValueKind.String);
        }

        public static JsonNode ToUInt64JsonNode(string propertyName, [NotNull] object? value)
        {
            CheckIncomingType<ulong>(propertyName, value);
            return JsonValue.Create((ulong)value);
        }

        public static ulong FromUInt64JsonNode(string propertyName, JsonNode value)
        {
            return FromJsonNode<ulong>(propertyName, value, JsonValueKind.Number);
        }

        public static JsonNode ToInt64JsonNode(string propertyName, [NotNull] object? value)
        {
            CheckIncomingType<long>(propertyName, value);
            return JsonValue.Create((long)value);
        }

        public static long FromInt64JsonNode(string propertyName, JsonNode value)
        {
            return FromJsonNode<long>(propertyName, value, JsonValueKind.Number);
        }

        public static JsonNode ToDoubleJsonNode(string propertyName, [NotNull] object? value)
        {
            CheckIncomingType<double>(propertyName, value);
            return JsonValue.Create((double)value);
        }

        public static double FromDoubleJsonNode(string propertyName, JsonNode value)
        {
            return FromJsonNode<double>(propertyName, value, JsonValueKind.Number);
        }

        public static JsonNode ToBooleanJsonNode(string propertyName, [NotNull] object? value)
        {
            CheckIncomingType<bool>(propertyName, value);
            return JsonValue.Create((bool)value);
        }

        public static bool FromBooleanJsonNode(string propertyName, JsonNode value)
        {
            if (value == null || value.GetValueKind() == JsonValueKind.Null)
            {
                throw new JsonValueWasNullException(propertyName);
            }

            if (value.GetValueKind() != JsonValueKind.True &&
                value.GetValueKind() != JsonValueKind.False)
            {
                throw new JsonValueWasIncorrectKindException(propertyName, value.GetValueKind(), JsonValueKind.True);
            }

            return value.GetValue<bool>();
        }
    }
}
