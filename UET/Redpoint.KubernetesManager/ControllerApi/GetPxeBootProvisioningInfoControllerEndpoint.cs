namespace Redpoint.KubernetesManager.ControllerApi
{
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class GetPxeBootProvisioningInfoControllerEndpoint : IControllerEndpoint
    {
        public string Path => "/pxe-boot-provisioning-info";

        public Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
