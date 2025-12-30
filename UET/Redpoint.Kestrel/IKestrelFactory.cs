namespace Redpoint.Kestrel
{
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IKestrelFactory
    {
        Task<KestrelServer> CreateAndStartServerAsync(
             KestrelServerOptions serverOptions,
             IKestrelRequestHandler requestHandler,
             CancellationToken cancellationToken);
    }
}
