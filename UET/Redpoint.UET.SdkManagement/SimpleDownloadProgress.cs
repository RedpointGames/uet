namespace Redpoint.UET.SdkManagement
{
    using Redpoint.ProgressMonitor;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SimpleDownloadProgress : ISimpleDownloadProgress
    {
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;

        public SimpleDownloadProgress(
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
            string downloadUrl,
            Func<Stream, Task> copier,
            CancellationToken cancellationToken)
        {
            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            using (var stream = new PositionAwareStream(
                await response.Content.ReadAsStreamAsync(),
                response.Content.Headers.ContentLength!.Value))
            {
                var cts = new CancellationTokenSource();
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
                        cts.Token);
                });

                await copier(stream);

                cts.Cancel();
                try
                {
                    await monitorTask;
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
}
