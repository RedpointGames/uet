namespace Redpoint.Uet.Uat.Internal
{
    using Redpoint.Windows.HandleManagement;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows6.2")]
    internal class NativeLocalHandleCloser : ILocalHandleCloser
    {
        public async Task CloseLocalHandles(string localPath)
        {
            await foreach (var fileHandle in NativeHandles.GetAllFileHandlesAsync(CancellationToken.None))
            {
                if (fileHandle.FilePath.StartsWith(localPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    await NativeHandles.ForciblyCloseHandleAsync(fileHandle, CancellationToken.None);
                }
            }
        }
    }
}

