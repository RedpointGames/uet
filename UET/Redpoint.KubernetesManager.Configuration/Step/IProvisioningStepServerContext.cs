namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using System.Net;

    public interface IProvisioningStepServerContext
    {
        IPAddress RemoteIpAddress { get; }
    }
}
