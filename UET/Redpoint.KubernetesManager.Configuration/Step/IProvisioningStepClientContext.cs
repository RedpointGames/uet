namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    public interface IProvisioningStepClientContext
    {
        bool IsLocalTesting { get; }

        HttpClient ProvisioningApiClient { get; }

        string ProvisioningApiEndpoint { get; }
    }
}
