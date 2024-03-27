namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    internal partial class JsonConfigJsonSerializerContext : JsonSerializerContext
    {
    }
}
