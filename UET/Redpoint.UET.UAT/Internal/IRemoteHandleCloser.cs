namespace Redpoint.UET.UAT.Internal
{
    using System.Threading.Tasks;

    internal interface IRemoteHandleCloser
    {
        Task<bool> CloseRemoteHandles(string Path);
    }
}
