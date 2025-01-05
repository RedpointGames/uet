namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    public enum JenkinsJobStatus
    {
        /// <summary>
        /// The job exists but is not building.
        /// </summary>
        Idle,

        /// <summary>
        /// The job has a queued build and is waiting for an executor to become available.
        /// </summary>
        Queued,

        /// <summary>
        /// The job is currently executing a build on an executor.
        /// </summary>
        Executing,

        /// <summary>
        /// The job has successfully completed building.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The job has successfully completed building.
        /// </summary>
        Failed,
    }
}
