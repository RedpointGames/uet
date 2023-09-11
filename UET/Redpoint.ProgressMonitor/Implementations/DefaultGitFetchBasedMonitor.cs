namespace Redpoint.ProgressMonitor.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DefaultGitFetchBasedMonitor : IGitFetchBasedMonitor
    {
        private readonly IUtilities _utilities;

        public DefaultGitFetchBasedMonitor(IUtilities utilities)
        {
            _utilities = utilities;
        }

        public string ComputeProgressMessage(IGitFetchBasedProgress progress, int? widthHint, DateTimeOffset startTime)
        {
            if (progress.TotalObjects != null)
            {
                var totalObjects = progress.TotalObjects ?? 0;
                var indexedObjects = progress.IndexedObjects ?? 0;
                var receivedObjects = progress.ReceivedObjects ?? 0;

                long units;
                double computedProgress;
                string status;
                if (receivedObjects == totalObjects)
                {
                    status = "indexing";
                    units = indexedObjects;
                }
                else
                {
                    status = "fetching";
                    units = receivedObjects;
                }

                if (progress.TotalObjects == 0)
                {
                    // No progress to compute.
                    computedProgress = 0;
                }
                else
                {
                    // Compute the weighted progress.
                    var indexingProgressWeight = progress.IndexingProgressWeight ?? 0.1;
                    if (indexingProgressWeight < 1.0)
                    {
                        computedProgress = (receivedObjects + indexedObjects * indexingProgressWeight) / (totalObjects * (1.0 + indexingProgressWeight)) * 100.0;
                    }
                    else if (indexingProgressWeight == 1.0)
                    {
                        computedProgress = (receivedObjects + indexedObjects) / (totalObjects * 2.0) * 100.0;
                    }
                    else if (indexingProgressWeight > 1.0)
                    {
                        computedProgress = ((receivedObjects * (1.0 / indexingProgressWeight)) + indexedObjects) / (totalObjects * (1.0 + indexingProgressWeight)) * 100.0;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                var currentTime = DateTimeOffset.UtcNow;
                var remainingTime = _utilities.ComputeRemainingTime(startTime, currentTime, computedProgress);

                long remainingSeconds = (long)remainingTime.TotalSeconds;
                long remainingMinutes = remainingSeconds / 60;
                remainingSeconds = remainingSeconds % 60;

                var bytesFetched = progress.ReceivedBytes ?? 0;
                var bytesFetchedInfo = string.Empty;
                if (bytesFetched > 0)
                {
                    bytesFetchedInfo = $", {_utilities.FormatDataAmount(bytesFetched)}";
                }

                return $"{status}...{progress.FetchContext} {computedProgress,9:#####0.00} % ({units,7:######0} / {totalObjects,7:######0}) objects{bytesFetchedInfo}, {remainingMinutes}:{remainingSeconds:00} to go";
            }
            else if (!string.IsNullOrWhiteSpace(progress.ServerProgressMessage))
            {
                return $"preparing...{progress.FetchContext} {0,9:#####0.00} % {(progress.ServerProgressMessage ?? string.Empty).ToLowerInvariant().Trim()}";
            }
            else
            {
                return $"preparing...{progress.FetchContext} {0,9:#####0.00} % ({0,7:######0} / {progress.TotalObjects ?? 0,7:######0}) objects, 0:00 to go";
            }
        }

        public async Task MonitorAsync(
            IGitFetchBasedProgress progress,
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