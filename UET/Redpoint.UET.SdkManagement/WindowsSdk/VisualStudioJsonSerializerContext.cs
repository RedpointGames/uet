namespace Redpoint.UET.SdkManagement.WindowsSdk
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    [JsonSerializable(typeof(VisualStudioManifest))]
    [JsonSerializable(typeof(VisualStudioManifestChannelItem))]
    [JsonSerializable(typeof(VisualStudioManifestChannelItemPayload))]
    [JsonSerializable(typeof(VisualStudioManifestPackageDependency))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class VisualStudioJsonSerializerContext : JsonSerializerContext
    {
    }
}
