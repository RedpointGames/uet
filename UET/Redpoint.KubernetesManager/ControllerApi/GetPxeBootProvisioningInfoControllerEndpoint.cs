namespace Redpoint.KubernetesManager.ControllerApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class GetPxeBootProvisioningInfoControllerEndpoint : IControllerEndpoint
    {
        public string Path => "/pxe-boot-provisioning-info";

        public Task HandleAsync(HttpListenerContext context)
        {
            throw new NotImplementedException();
        }
    }
}
