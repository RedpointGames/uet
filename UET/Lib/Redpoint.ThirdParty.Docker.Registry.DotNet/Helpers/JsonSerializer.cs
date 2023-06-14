namespace Docker.Registry.DotNet.Helpers
{
    using System.Text.Json.Serialization.Metadata;

    internal class JsonSerializer
    {
        public T DeserializeObject<T>(string json, JsonTypeInfo<T> typeInfo)
        {
            return System.Text.Json.JsonSerializer.Deserialize(
                json,
                typeInfo)!;
        }

        public string SerializeObject<T>(T value, JsonTypeInfo<T> typeInfo)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                value,
                typeInfo)!;
        }
    }
}