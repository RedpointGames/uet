namespace Redpoint.Uet.Uat.Internal
{
    using System.Threading.Tasks;

    internal interface ILocalHandleCloser
    {
        Task CloseLocalHandles(string localPath);
    }
}
