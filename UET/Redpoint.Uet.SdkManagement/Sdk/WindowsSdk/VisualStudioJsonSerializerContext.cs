namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(VisualStudioManifest))]
    [JsonSerializable(typeof(VisualStudioManifestChannelItem))]
    [JsonSerializable(typeof(VisualStudioManifestChannelItemPayload))]
    [JsonSerializable(typeof(VisualStudioManifestPackageDependency))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class VisualStudioJsonSerializerContext : JsonSerializerContext
    {
    }
}
