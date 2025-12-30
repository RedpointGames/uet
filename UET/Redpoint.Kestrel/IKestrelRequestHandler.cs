namespace Redpoint.Kestrel
{
    using Microsoft.AspNetCore.Http;
    using System.Threading.Tasks;

    public interface IKestrelRequestHandler
    {
        Task HandleRequestAsync(HttpContext httpContext);
    }
}
