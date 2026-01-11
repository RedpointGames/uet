namespace Redpoint.KubernetesManager.PxeBoot.Provisioning
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IProvisioningStateManager
    {
        Task<ProvisioningResponse> UpdateStateAsync(
            INodeProvisioningContext context,
            bool canClearForceReprovisionFlag);

        Task<bool> ResetProvisioningStateAndReturnTrueIfRebootRequiredAsync(
            INodeProvisioningContext context,
            bool forceReboot,
            bool canClearForceReprovisionFlag);

        void UpdateRegisteredIpAddressesForNode(
            INodeProvisioningContext context);

        void MarkProvisioningCompleteForNode(
            INodeProvisioningContext context);
    }
}
