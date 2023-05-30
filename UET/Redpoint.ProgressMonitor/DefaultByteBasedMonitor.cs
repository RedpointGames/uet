namespace Redpoint.ProgressMonitor
{
    internal class DefaultByteBasedMonitor : IByteBasedMonitor
    {
        public async Task MonitorAsync(
            IByteBasedProgress progress,
            IConsoleInformation? consoleInfo,
            ProgressEmitDelegate onProgressEmit,
            CancellationToken ct)
        {
            var startTime = DateTimeOffset.UtcNow;
            int count = 0;
            while (!ct.IsCancellationRequested)
            {
                double bytesProgress = (progress.Position / (double)progress.Length) * 100.0;

                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                TimeSpan remainingTime = TimeSpan.FromSeconds(100);
                if (bytesProgress > 0)
                {
                    double bytesRatio = bytesProgress / 100.0;
                    long msElapsed = currentTime.ToUnixTimeMilliseconds() - startTime.ToUnixTimeMilliseconds();
                    //        x                1.0
                    //  -------------- = ---------------
                    //    ms_elapsed       bytes_ratio
                    long msEstimated = (long)((1.0 / bytesRatio) * msElapsed);
                    remainingTime = TimeSpan.FromMilliseconds(msEstimated - msElapsed);
                }

                Int64 remainingSeconds = (long)remainingTime.TotalSeconds;
                Int64 remainingMinutes = remainingSeconds / 60;
                remainingSeconds = remainingSeconds % 60;

                double bytesPerSecond =
                    progress.Position /
                    (currentTime - startTime).TotalSeconds;

                string rate = "b/s";
                if (bytesPerSecond > 1024)
                {
                    bytesPerSecond /= 1024;
                    rate = "KB/s";
                }
                if (bytesPerSecond > 1024)
                {
                    bytesPerSecond /= 1024;
                    rate = "MB/s";
                }
                if (bytesPerSecond > 1024)
                {
                    bytesPerSecond /= 1024;
                    rate = "GB/s";
                }

                count++;

                onProgressEmit(
                    $"progress: {bytesProgress.ToString("#####0.00").PadLeft(9)} % ({(progress.Position / 1024 / 1024).ToString("######0").PadLeft(7)} / {(progress.Length / 1024 / 1024).ToString("######0")}) MB, {bytesPerSecond.ToString("#####0.00")} {rate}, {remainingMinutes}:{remainingSeconds.ToString("00")} to go",
                    count);

                try
                {
                    await Task.Delay(100, ct);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }
    }
}