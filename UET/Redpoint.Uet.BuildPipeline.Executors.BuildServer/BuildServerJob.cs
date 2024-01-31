namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using System.Collections.Generic;

    public record class BuildServerJob
    {
        /// <summary>
        /// The name of this build job.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The stage this build job runs in.
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// The names of other build jobs that this build job depends on.
        /// </summary>
        public IReadOnlyCollection<string> Needs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The platform for this build job to run on.
        /// </summary>
        public BuildServerJobPlatform Platform { get; set; } = BuildServerJobPlatform.Windows;

        /// <summary>
        /// If true, this build job is a manual build job.
        /// </summary>
        public bool IsManual { get; set; } = false;

        /// <summary>
        /// A list of steps to run inside this job.
        /// </summary>
        public IList<BuildServerJobStep> JobSteps { get; } = new List<BuildServerJobStep>();
    }
}
