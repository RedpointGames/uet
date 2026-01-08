namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System.Net;

    internal class IpxeProvisioningStepServerContext : IProvisioningStepServerContext
    {
        private readonly IPAddress _remoteIpAddress;

        public IpxeProvisioningStepServerContext(IPAddress remoteIpAddress)
        {
            _remoteIpAddress = remoteIpAddress;
        }

        public IPAddress RemoteIpAddress => _remoteIpAddress;
    }
}
