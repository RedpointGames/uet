namespace Redpoint.KubernetesManager.PxeBoot.Server.Handlers
{
    using System.Net;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal interface IPxeBootTftpRequestHandler
    {
        Task HandleRequestAsync(
            PxeBootServerContext serverContext,
            ITftpTransfer transfer,
            EndPoint client);
    }
}
