namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoList<T>
    {
        [JsonPropertyName("meta"), JsonRequired]
        public PlivoListMeta? Meta { get; set; }

        [JsonPropertyName("objects"), JsonRequired]
        public T[]? Objects { get; set; }
    }
}
