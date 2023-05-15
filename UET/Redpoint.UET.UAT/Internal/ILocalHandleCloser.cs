namespace Redpoint.UET.UAT.Internal
{
    using System.Threading.Tasks;

    internal interface ILocalHandleCloser
    {
        Task CloseLocalHandles(string localPath);
    }
}
