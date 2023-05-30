namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a monitor which receives progress information and computes progress messages.
    /// </summary>
    /// <typeparam name="T">The type of progress which is monitored.</typeparam>
    public interface IMonitor<T>
    {
        /// <summary>
        /// Monitors the progress object until the cancellation token is cancelled. You must cancel the token in order for this function to return.
        /// </summary>
        /// <param name="progress">The progress object to monitor.</param>
        /// <param name="consoleInfo">The console information, if provided.</param>
        /// <param name="onProgressEmit">Called by the monitor with the reported progress of the operation.</param>
        /// <param name="ct">The cancellation token used to stop monitoring.</param>
        /// <returns>The monitor task, which should only be awaited after the cancellation token has been cancelled and the target operation has finished.</returns>
        Task MonitorAsync(
            T progress,
            IConsoleInformation? consoleInfo,
            ProgressEmitDelegate onProgressEmit,
            CancellationToken ct);
    }
}