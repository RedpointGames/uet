namespace Redpoint.KubernetesManager.Services.Kestrel
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;
    using static System.Net.Mime.MediaTypeNames;

    internal interface IKestrelFactory
    {
        Task<KestrelServer> CreateAndStartServerAsync(
             KestrelServerOptions serverOptions,
             IKestrelRequestHandler requestHandler,
             CancellationToken cancellationToken);
    }
}
