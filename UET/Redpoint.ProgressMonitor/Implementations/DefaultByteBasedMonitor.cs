namespace Redpoint.ProgressMonitor.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultByteBasedMonitor : IByteBasedMonitor
    {
        private readonly IUtilities _utilities;

        public DefaultByteBasedMonitor(IUtilities utilities)
        {
            _utilities = utilities;
        }

        public string ComputeProgressMessage(
            IByteBasedProgress progress,
            int? widthHint,
            DateTimeOffset startTime)
        {
            double bytesProgress = (progress.Position / (double)progress.Length) * 100.0;

            var currentTime = DateTimeOffset.UtcNow;
            var remainingTime = _utilities.ComputeRemainingTime(startTime, currentTime, bytesProgress);

            long remainingSeconds = (long)remainingTime.TotalSeconds;
            long remainingMinutes = remainingSeconds / 60;
            remainingSeconds = remainingSeconds % 60;

            double bytesPerSecond =
                progress.Position /
                (currentTime - startTime).TotalSeconds;

            var rate = _utilities.FormatDataTransferRate(bytesPerSecond);

            return $"{bytesProgress,9:#####0.00} % ({progress.Position / 1024 / 1024,7:######0} /{progress.Length / 1024 / 1024,7:######0}) MB, {rate}, {remainingMinutes}:{remainingSeconds:00} to go";
        }

        public async Task MonitorAsync(
            IByteBasedProgress progress,
            IConsoleInformation? consoleInfo,
            ProgressEmitDelegate onProgressEmit,
            CancellationToken ct)
        {
            var startTime = DateTimeOffset.UtcNow;
            int count = 0;
            var doOnceMore = false;
            while (!ct.IsCancellationRequested || doOnceMore)
            {
                doOnceMore = false;

                int? targetWidth = null;
                if (consoleInfo?.Width != null)
                {
                    targetWidth = consoleInfo.Width - "progress: ".Length;
                }

                count++;
                onProgressEmit(
                    $"progress: {ComputeProgressMessage(progress, targetWidth, startTime)}",
                    count);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(100, ct);
                }
                catch (OperationCanceledException)
                {
                    // Make sure we go around once more.
                    doOnceMore = true;
                }
            }
        }
    }
}