namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using System.Collections.Generic;

    public record class BuildServerPipeline
    {
        /// <summary>
        /// A list of stages in this pipeline.
        /// </summary>
        public HashSet<string> Stages = new HashSet<string>();

        /// <summary>
        /// The list of jobs to execute in this pipeline.
        /// </summary>
        public Dictionary<string, BuildServerJob> Jobs = new Dictionary<string, BuildServerJob>();

        /// <summary>
        /// The global environment variables to apply to all jobs.
        /// </summary>
        public Dictionary<string, string> GlobalEnvironmentVariables { get; set; } = new Dictionary<string, string>();
    }
}
