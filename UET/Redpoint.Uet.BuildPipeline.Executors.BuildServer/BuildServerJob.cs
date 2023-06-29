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
        public string[] Needs { get; set; } = new string[0];

        /// <summary>
        /// The platform for this build job to run on.
        /// </summary>
        public BuildServerJobPlatform Platform { get; set; } = BuildServerJobPlatform.Windows;

        /// <summary>
        /// If true, this build job is a manual build job.
        /// </summary>
        public bool IsManual { get; set; } = false;

        /// <summary>
        /// A list of artifact paths to upload for this build job.
        /// </summary>
        public string[] ArtifactPaths { get; set; } = new string[0];

        /// <summary>
        /// The path to the JUnit test report to upload for this build job.
        /// </summary>
        public string? ArtifactJUnitReportPath { get; set; } = null;

        /// <summary>
        /// The environment variables to set for this build job.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The PowerShell or Bash script to run for this build job (depending on the platform).
        /// </summary>
        public string Script { get; set; } = string.Empty;

        /// <summary>
        /// The PowerShell or Bash script to run after this build job (this should always execute, even if the build job script fails).
        /// </summary>
        public string? AfterScript { get; set; } = null;
    }
}
