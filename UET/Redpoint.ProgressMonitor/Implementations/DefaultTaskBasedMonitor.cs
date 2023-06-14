namespace Redpoint.ProgressMonitor.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultTaskBasedMonitor : ITaskBasedMonitor
    {
        public string ComputeProgressMessage(ITaskBasedProgress progress, int? widthHint, DateTimeOffset startTime)
        {
            var taskProgressPrefix = string.Empty;
            if (progress.CurrentTaskIndex.HasValue && progress.TotalTasks.HasValue)
            {
                taskProgressPrefix = $"({progress.CurrentTaskIndex}/{progress.TotalTasks}) ";
            }

            var message = $"{taskProgressPrefix}{progress.CurrentTaskStatus}";
            int? nestedWidthHint = null;
            if (widthHint != null)
            {
                nestedWidthHint = widthHint - (message.Length + 2);
            }

            if (progress.ByteBasedProgress != null && progress.ByteBasedMonitor != null)
            {
                message += $": {progress.ByteBasedMonitor.ComputeProgressMessage(progress.ByteBasedProgress, nestedWidthHint, progress.CurrentTaskStartTime)}";
            }
            else if (progress.GitFetchBasedProgress != null && progress.GitFetchBasedMonitor != null)
            {
                message += $": {progress.GitFetchBasedMonitor.ComputeProgressMessage(progress.GitFetchBasedProgress, nestedWidthHint, progress.CurrentTaskStartTime)}";
            }

            return message;
        }

        public async Task MonitorAsync(ITaskBasedProgress progress, IConsoleInformation? consoleInfo, ProgressEmitDelegate onProgressEmit, CancellationToken ct)
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