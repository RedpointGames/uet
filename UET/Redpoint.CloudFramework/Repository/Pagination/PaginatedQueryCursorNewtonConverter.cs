namespace Redpoint.CloudFramework.Repository.Pagination
{
    using System;

    public class PaginatedQueryCursorNewtonConverter : Newtonsoft.Json.JsonConverter<PaginatedQueryCursor>
    {
        public override PaginatedQueryCursor ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, PaginatedQueryCursor? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(reader);

            if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
            {
                return new PaginatedQueryCursor(reader.ReadAsString());
            }
            else
            {
                reader.Read();
                return new PaginatedQueryCursor(string.Empty);
            }
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, PaginatedQueryCursor? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue((string)value!);
            }
        }
    }
}
