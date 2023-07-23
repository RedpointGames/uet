namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CloudflareResult<CloudflareWorker>))]
    [JsonSerializable(typeof(CloudflareResult<CloudflareWorker[]>))]
    [JsonSerializable(typeof(CloudflareResult<CloudflareSubdomain>))]
    [JsonSerializable(typeof(CloudflareResult<CloudflareKvNamespace>))]
    [JsonSerializable(typeof(CloudflareResult<CloudflareKvNamespace[]>))]
    [JsonSerializable(typeof(CloudflareResult<object>))]
    [JsonSerializable(typeof(CloudflareSubdomainEnable))]
    [JsonSerializable(typeof(CloudflareKvNamespaceCreate))]
    internal partial class CloudflareJsonSerializerContext : JsonSerializerContext
    {
    }
}
