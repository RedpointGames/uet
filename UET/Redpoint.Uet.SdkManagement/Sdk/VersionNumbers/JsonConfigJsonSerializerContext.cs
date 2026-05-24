namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    [JsonSerializable(typeof(EngineBuildVersion))]
    internal partial class JsonConfigJsonSerializerContext : JsonSerializerContext
    {
    }
}
