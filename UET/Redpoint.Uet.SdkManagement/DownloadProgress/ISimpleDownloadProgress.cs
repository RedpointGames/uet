namespace Redpoint.Uet.SdkManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISimpleDownloadProgress
    {
        Task DownloadAndCopyToStreamAsync(
            HttpClient client,
            Uri downloadUrl,
            Func<Stream, Task> copier,
            CancellationToken cancellationToken);
    }
}
