namespace UET.Commands.Internal.VerifyDllFileIntegrity
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class EnvsJsonSerializerContext : JsonSerializerContext
    {
    }
}
