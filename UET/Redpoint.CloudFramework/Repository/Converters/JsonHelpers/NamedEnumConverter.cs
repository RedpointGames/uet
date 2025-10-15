namespace Redpoint.CloudFramework.Repository.Converters.JsonHelpers
{
    using Redpoint.CloudFramework.Infrastructure;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class NamedEnumConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type objectType)
        {
            var t = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)!
                : objectType;

            return t.IsEnum;
        }

        public override object? Read(
            ref Utf8JsonReader reader, 
            Type objectType, 
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                if (!(objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    throw new JsonException("Cannot convert null value to " + objectType.Name);
                }

                return null;
            }

            var enumType = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)!
                : objectType;

            var enumTypeNamedAttribute = enumType.GetCustomAttributes(typeof(INamedEnum)).Cast<INamedEnum>().FirstOrDefault();
            if (enumTypeNamedAttribute == null || enumTypeNamedAttribute.EnumType != enumType)
            {
                throw new JsonException($"{enumType.FullName} is missing the [NamedEnum<{enumType.Name}>] attribute, or its type parameter is incorrect.");
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var memberInfos = enumTypeNamedAttribute.EnumType.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (var memberInfo in memberInfos)
                {
                    var attributes = memberInfo.GetCustomAttributes(typeof(NamedEnumValueAttribute), false);
                    var namedValue = ((NamedEnumValueAttribute)attributes[0]).Name;

                    if (namedValue == reader.GetString())
                    {
                        return Enum.Parse(enumTypeNamedAttribute.EnumType, memberInfo.Name);
                    }
                }

                throw new JsonException("Unable to find mapped value for '" + reader.GetString() + "'.");
            }

            throw new JsonException("Unexpected token when parsing enum.");
        }

        [SuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.", Justification = "This implementation only accesses enumeration members that will have already been accessed when passed into the 'value' argument.")]
        public override void Write(
            Utf8JsonWriter writer, 
            object value, 
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var objectType = value.GetType();
            var enumType = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)
                : objectType;
            if (enumType == null)
            {
                writer.WriteNullValue();
                return;
            }

            var fieldName = Enum.GetName(enumType, value);
            if (fieldName == null)
            {
                writer.WriteNullValue();
                return;
            }

            var memberInfo = enumType.GetMember(fieldName);
            var attributes = memberInfo[0].GetCustomAttributes(typeof(NamedEnumValueAttribute), false);
            var namedValue = ((NamedEnumValueAttribute)attributes[0]).Name;

            writer.WriteStringValue(namedValue);
        }
    }
}
