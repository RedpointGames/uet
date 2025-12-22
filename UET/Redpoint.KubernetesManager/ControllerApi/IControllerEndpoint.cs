namespace Redpoint.KubernetesManager.ControllerApi
{
    using Microsoft.AspNetCore.Http;
    using System.Net;
    using System.Threading.Tasks;

    internal interface IControllerEndpoint
    {
        string Path { get; }

        Task HandleAsync(HttpContext context, CancellationToken cancellationToken);
    }
}
