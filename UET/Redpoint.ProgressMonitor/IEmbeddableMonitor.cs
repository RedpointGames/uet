namespace Redpoint.ProgressMonitor
{
    using System;

    /// <summary>
    /// Represnts a monitor which can be embedded into the progress reports of other monitors.
    /// </summary>
    /// <typeparam name="T">The type of progress which is monitored.</typeparam>
    public interface IEmbeddableMonitor<T> : IMonitor<T>
    {
        /// <summary>
        /// Computes a progress message which can be embedded in another monitor.
        /// </summary>
        /// <param name="progress">The progress to compute the message for.</param>
        /// <param name="widthHint">The width to target for the message, if known.</param>
        /// <param name="startTime">The start time of the overall operation.</param>
        /// <returns>The computed progress message.</returns>
        string ComputeProgressMessage(
            T progress,
            int? widthHint,
            DateTimeOffset startTime);
    }
}