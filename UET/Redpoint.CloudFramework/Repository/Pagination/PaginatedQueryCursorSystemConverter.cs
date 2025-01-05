namespace Redpoint.CloudFramework.Repository.Pagination
{
    using System;

    public class PaginatedQueryCursorSystemConverter : System.Text.Json.Serialization.JsonConverter<PaginatedQueryCursor>
    {
        public override PaginatedQueryCursor Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.String)
            {
                return new PaginatedQueryCursor(reader.GetString());
            }
            else
            {
                reader.Read();
                return new PaginatedQueryCursor(string.Empty);
            }
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, PaginatedQueryCursor value, System.Text.Json.JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }
}
