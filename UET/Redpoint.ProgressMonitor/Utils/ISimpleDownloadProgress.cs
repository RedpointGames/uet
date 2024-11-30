#if NETCOREAPP

namespace Redpoint.ProgressMonitor.Utils
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A utility interface which downloads a URL to a stream and emits progress information to the console.
    /// </summary>
    public interface ISimpleDownloadProgress
    {
        /// <summary>
        /// Download the target URL using the specified <see cref="HttpClient"/>, passing the download stream to <paramref name="copier"/> (which is typically a lambda that calls <see cref="Stream.CopyToAsync(Stream, CancellationToken)"/>).
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to use for the download.</param>
        /// <param name="downloadUrl">The URL to download.</param>
        /// <param name="copier">The asynchronous function that the download stream will be passed to. When the returned task completes, the stream should have been fully written or consumed.</param>
        /// <param name="cancellationToken">A token which can be used to cancel the download.</param>
        /// <returns>An awaitable task.</returns>
        Task DownloadAndCopyToStreamAsync(
            HttpClient client,
            Uri downloadUrl,
            Func<Stream, Task> copier,
            CancellationToken cancellationToken);
    }
}

#endif