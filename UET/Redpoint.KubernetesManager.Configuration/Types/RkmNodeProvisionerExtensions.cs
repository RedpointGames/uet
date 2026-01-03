namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    public static class RkmNodeProvisionerExtensions
    {
        public static string GetHash(this RkmNodeProvisioner provisioner, JsonTypeInfo jsonTypeInfo)
        {
            return Convert.ToHexStringLower(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(provisioner, jsonTypeInfo))));
        }
    }
}
