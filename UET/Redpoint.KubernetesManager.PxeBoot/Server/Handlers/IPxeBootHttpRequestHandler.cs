namespace Redpoint.KubernetesManager.PxeBoot.Server.Handlers
{
    using Microsoft.AspNetCore.Http;
    using System.Threading.Tasks;

    internal interface IPxeBootHttpRequestHandler
    {
        Task HandleRequestAsync(
            PxeBootServerContext serverContext,
            HttpContext httpContext);
    }
}
