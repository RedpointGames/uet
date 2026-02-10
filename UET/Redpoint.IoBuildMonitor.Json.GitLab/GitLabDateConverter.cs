using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;

namespace Io.Json.GitLab
{
    public class GitLabDateConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                string stringValue = reader.GetString() ?? string.Empty;
                var dateValue = DateTimeOffset.ParseExact(
                    stringValue.Substring(0, "yyyy-MM-dd HH:mm:ss".Length),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);
                return dateValue;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }
        }
    }
}
