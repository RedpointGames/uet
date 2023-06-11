namespace Redpoint.UET.UAT.Internal
{
    using System.Threading.Tasks;

    internal class NullLocalHandleCloser : ILocalHandleCloser
    {
        public Task CloseLocalHandles(string localPath)
        {
            return Task.CompletedTask;
        }
    }
}

