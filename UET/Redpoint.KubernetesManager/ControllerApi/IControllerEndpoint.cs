namespace Redpoint.KubernetesManager.ControllerApi
{
    using System.Net;
    using System.Threading.Tasks;

    internal interface IControllerEndpoint
    {
        string Path { get; }

        Task HandleAsync(HttpListenerContext context);
    }
}
