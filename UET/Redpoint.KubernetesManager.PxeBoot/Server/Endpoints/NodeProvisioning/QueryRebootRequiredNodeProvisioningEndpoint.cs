namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class QueryRebootRequiredNodeProvisioningEndpoint : StepBaseNodeProvisioningEndpoint
    {
        public QueryRebootRequiredNodeProvisioningEndpoint(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public override string Path => "/query-reboot-required";

        protected override async Task HandleStepRequestAsync(
            INodeProvisioningEndpointContext context,
            IProvisioningStepServerContext serverContext,
            RkmNodeProvisionerStep currentStep,
            IProvisioningStep provisioningStepImpl)
        {
            // StepBaseNodeProvisioningEndpoint will have already returned NoContent
            // if the client is up to date. If we get to this point, there's provisioning
            // work to do, and the machine should reboot.

            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(context.AikFingerprintShort, context.CancellationToken);
            return;
        }
    }
}
