namespace Redpoint.IoBuildMonitor.Mappers
{
    using Io.Json.GitLab;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(BridgeJson))]
    [JsonSerializable(typeof(BridgeJson[]))]
    [JsonSerializable(typeof(BuildWebhookJson))]
    [JsonSerializable(typeof(PipelineWebhookJson))]
    public partial class IoJsonSerializerContext : JsonSerializerContext
    {
    }
}
