namespace Redpoint.RuntimeJson
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    public interface IRuntimeJson
    {
        Type Type { get; }

        JsonTypeInfo JsonTypeInfo { get; }

        JsonSerializerContext JsonSerializerContext { get; }

        object Deserialize(ref Utf8JsonReader reader);

        void Serialize(Utf8JsonWriter writer, object value);
    }

    public interface IRuntimeJson<T> : IRuntimeJson
    {
    }
}