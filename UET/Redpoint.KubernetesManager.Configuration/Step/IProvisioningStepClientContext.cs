namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.Configuration.Types;

    public interface IProvisioningStepClientContext
    {
        bool IsLocalTesting { get; }

        HttpClient ProvisioningApiClient { get; }

        string ProvisioningApiEndpointHttps { get; }

        string ProvisioningApiEndpointHttp { get; }

        string ProvisioningApiAddress { get; }

        string AuthorizedNodeName { get; }

        string AikFingerprint { get; }

        Dictionary<string, string> ParameterValues { get; }

        string? DiskPathLinux { get; }

        ProvisioningClientPlatformType Platform { get; }
    }
}
