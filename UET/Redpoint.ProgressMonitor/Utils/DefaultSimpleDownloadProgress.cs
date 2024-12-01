#if NETCOREAPP

namespace Redpoint.ProgressMonitor.Utils
{
    using Redpoint.ProgressMonitor;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultSimpleDownloadProgress : ISimpleDownloadProgress
    {
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;

        public DefaultSimpleDownloadProgress(
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory)
        {
            _progressFactory = progressFactory;
            _monitorFactory = monitorFactory;
        }

        private class DidOutput
        {
            public bool Did;
        }

        public async Task DownloadAndCopyToStreamAsync(
            HttpClient client,
            Uri downloadUrl,
            Func<Stream, Task> copier,
            CancellationToken cancellationToken)
        {
            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            using var stream = new PositionAwareStream(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                response.Content.Headers.ContentLength!.Value);

            using var cts = new CancellationTokenSource();
            var progress = _progressFactory.CreateProgressForStream(stream);
            var outputTrack = new DidOutput();
            var monitorTask = Task.Run(async () =>
            {
                var consoleWidth = 0;
                try
                {
                    consoleWidth = Console.BufferWidth;
                }
                catch { }

                var monitor = _monitorFactory.CreateByteBasedMonitor();
                await monitor.MonitorAsync(
                    progress,
                    null,
                    (message, count) =>
                    {
                        if (consoleWidth != 0)
                        {
                            Console.Write($"\r                {message}".PadRight(consoleWidth));
                            outputTrack.Did = true;
                        }
                        else if (count % 50 == 0)
                        {
                            Console.WriteLine($"                {message}");
                            outputTrack.Did = true;
                        }
                    },
                    cts.Token).ConfigureAwait(false);
            }, cts.Token);

            await copier(stream).ConfigureAwait(false);

            cts.Cancel();
            try
            {
                await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            if (outputTrack.Did)
            {
                var consoleWidth = 0;
                try
                {
                    consoleWidth = Console.BufferWidth;
                }
                catch { }
                if (consoleWidth != 0)
                {
                    Console.WriteLine();
                }
            }
        }
    }
}

#endif