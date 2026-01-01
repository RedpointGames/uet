namespace Redpoint.KubernetesManager.Tpm.Negotiate
{
    using Redpoint.Tpm.Negotiate;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(NegotiateCertificateRequest))]
    [JsonSerializable(typeof(NegotiateCertificateResponse))]
    [JsonSerializable(typeof(NegotiateCertificateResponseBundle))]
    internal partial class NegotiateJsonSerializerContext : JsonSerializerContext
    {
    }
}
