namespace Redpoint.KubernetesManager.PxeBoot.Variable
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using static Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning.AuthorizeNodeProvisioningEndpoint;

    internal interface IVariableProvider
    {
        string SubstituteVariables(
            IProvisioningStepClientContext context,
            string content,
            Dictionary<string, string>? stepValues = null);

        Dictionary<string, string> GetEnvironmentVariables(
            IProvisioningStepClientContext context,
            Dictionary<string, string>? stepValues = null);

        Dictionary<string, string> ComputeParameterValuesNodeProvisioningEndpoint(ServerSideVariableContext context);
    }
}
