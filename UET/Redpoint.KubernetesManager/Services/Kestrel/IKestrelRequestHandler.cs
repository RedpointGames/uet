namespace Redpoint.KubernetesManager.Services.Kestrel
{
    using Microsoft.AspNetCore.Http;
    using System.Threading.Tasks;

    internal interface IKestrelRequestHandler
    {
        Task HandleRequestAsync(HttpContext httpContext);
    }
}
