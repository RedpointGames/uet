namespace Redpoint.ProgressMonitor
{
    using System;

    /// <summary>
    /// Represents a progress object that proceeds through multiple tasks (stages) to report on.
    /// </summary>
    public interface ITaskBasedProgress
    {
        /// <summary>
        /// The current task in progress.
        /// </summary>
        string CurrentTaskStatus { get; }

        /// <summary>
        /// The time that the current task was started.
        /// </summary>
        DateTimeOffset CurrentTaskStartTime { get; }

        /// <summary>
        /// The one-indexed number of the current task, if the total number of tasks is known.
        /// </summary>
        int? CurrentTaskIndex { get; }

        /// <summary>
        /// The total number of tasks, if the total number is known.
        /// </summary>
        int? TotalTasks { get; }

        /// <summary>
        /// If this and <see cref="ByteBasedMonitor"/>, the byte-based progress is shown.
        /// </summary>
        IByteBasedProgress? ByteBasedProgress { get; }

        /// <summary>
        /// If this and <see cref="ByteBasedProgress"/>, the byte-based progress is shown.
        /// </summary>
        IByteBasedMonitor? ByteBasedMonitor { get; }

        /// <summary>
        /// If this and <see cref="GitFetchBasedMonitor"/>, the Git fetch progress is shown.
        /// </summary>
        IGitFetchBasedProgress? GitFetchBasedProgress { get; }

        /// <summary>
        /// If this and <see cref="GitFetchBasedProgress"/>, the Git fetch progress is shown.
        /// </summary>
        IGitFetchBasedMonitor? GitFetchBasedMonitor { get; }
    }
}