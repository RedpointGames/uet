namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(WindowsRkmProvisionContext))]
    internal partial class WindowsRkmProvisionJsonSerializerContext : JsonSerializerContext
    {
    }
}
