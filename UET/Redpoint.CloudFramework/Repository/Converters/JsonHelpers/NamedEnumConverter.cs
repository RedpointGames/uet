namespace Redpoint.CloudFramework.Repository.Converters.JsonHelpers
{
    using Newtonsoft.Json;
    using Redpoint.CloudFramework.Infrastructure;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    internal class NamedEnumConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            var t = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)!
                : objectType;

            return t.IsEnum;
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (!(objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    throw new JsonSerializationException("Cannot convert null value to " + objectType.Name);
                }

                return null;
            }

            var enumType = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)!
                : objectType;

            var enumTypeNamedAttribute = enumType.GetCustomAttributes(typeof(INamedEnum)).Cast<INamedEnum>().FirstOrDefault();
            if (enumTypeNamedAttribute == null || enumTypeNamedAttribute.EnumType != enumType)
            {
                throw new JsonSerializationException($"{enumType.FullName} is missing the [NamedEnum<{enumType.Name}>] attribute, or its type parameter is incorrect.");
            }

            if (reader.TokenType == JsonToken.String)
            {
                var memberInfos = enumTypeNamedAttribute.EnumType.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (var memberInfo in memberInfos)
                {
                    var attributes = memberInfo.GetCustomAttributes(typeof(NamedEnumValueAttribute), false);
                    var namedValue = ((NamedEnumValueAttribute)attributes[0]).Name;

                    if (namedValue == reader.Value!.ToString())
                    {
                        return Enum.Parse(enumTypeNamedAttribute.EnumType, memberInfo.Name);
                    }
                }

                throw new JsonSerializationException("Unable to find mapped value for '" + reader.Value!.ToString() + "'.");
            }

            throw new JsonSerializationException("Unexpected token when parsing enum.");
        }

        [SuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.", Justification = "This implementation only accesses enumeration members that will have already been accessed when passed into the 'value' argument.")]
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var objectType = value.GetType();
            var enumType = (objectType.IsValueType && objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                ? Nullable.GetUnderlyingType(objectType)
                : objectType;
            if (enumType == null)
            {
                writer.WriteNull();
                return;
            }

            var fieldName = Enum.GetName(enumType, value);
            if (fieldName == null)
            {
                writer.WriteNull();
                return;
            }

            var memberInfo = enumType.GetMember(fieldName);
            var attributes = memberInfo[0].GetCustomAttributes(typeof(NamedEnumValueAttribute), false);
            var namedValue = ((NamedEnumValueAttribute)attributes[0]).Name;

            writer.WriteValue(namedValue);
        }
    }
}
