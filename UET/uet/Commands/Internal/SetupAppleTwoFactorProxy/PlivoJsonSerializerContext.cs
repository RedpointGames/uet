namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(PlivoApplication))]
    [JsonSerializable(typeof(PlivoList<PlivoApplication>))]
    [JsonSerializable(typeof(PlivoApplicationCreateRequest))]
    [JsonSerializable(typeof(PlivoApplicationCreateResponse))]
    [JsonSerializable(typeof(PlivoApplicationUpdateRequest))]
    [JsonSerializable(typeof(PlivoNumber))]
    [JsonSerializable(typeof(PlivoList<PlivoNumber>))]
    [JsonSerializable(typeof(PlivoNumberUpdateRequest))]
    internal partial class PlivoJsonSerializerContext : JsonSerializerContext
    {
    }
}
