namespace Redpoint.UET.UAT.Internal
{
    using System.Threading.Tasks;

    internal class DefaultRemoteHandleCloser : IRemoteHandleCloser
    {
        public async Task<bool> CloseRemoteHandles(string Path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            // @todo
            return false;
        }
    }
}
