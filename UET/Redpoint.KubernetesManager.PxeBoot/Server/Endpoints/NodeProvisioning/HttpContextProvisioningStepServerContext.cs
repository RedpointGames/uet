namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System.Net;

    internal class HttpContextProvisioningStepServerContext : IProvisioningStepServerContext
    {
        private readonly HttpContext _httpContext;

        public HttpContextProvisioningStepServerContext(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public IPAddress RemoteIpAddress => _httpContext.Connection.RemoteIpAddress;
    }
}
