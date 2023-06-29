namespace Redpoint.Uet.Uat.Internal
{
    using System.Threading.Tasks;

    internal class DefaultRemoteHandleCloser : IRemoteHandleCloser
    {
        public Task<bool> CloseRemoteHandles(string Path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult(false);
            }

            // @todo
            return Task.FromResult(false);
        }
    }
}
