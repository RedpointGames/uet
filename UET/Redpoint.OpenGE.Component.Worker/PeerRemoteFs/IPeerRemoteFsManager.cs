namespace Redpoint.OpenGE.Component.Worker.PeerRemoteFs
{
    using System.Net;
    using System.Threading.Tasks;

    internal interface IPeerRemoteFsManager
    {
        ValueTask<IPeerRemoteFsHandle> AcquirePeerRemoteFs(
            IPAddress ipAddress,
            int port,
            string[] additionalReparsePoints);
    }
}
