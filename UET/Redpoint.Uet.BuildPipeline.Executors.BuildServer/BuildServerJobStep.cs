namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using System.Collections.Generic;

    public record class BuildServerJobStep
    {
        /// <summary>
        /// A list of artifact paths to upload for this build job.
        /// </summary>
        public IReadOnlyList<string> ArtifactPaths { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The path to the JUnit test report to upload for this build job.
        /// </summary>
        public string? ArtifactJUnitReportPath { get; set; } = null;

        /// <summary>
        /// The environment variables to set for this build job.
        /// </summary>
        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// A delegate which generates the command to execute for this step, based on the executor name passed in.
        /// </summary>
        public BuildServerJobStepScript Script { get; set; } = _ => string.Empty;

        /// <summary>
        /// The PowerShell or Bash script to run after this build job (this should always execute, even if the build job script fails).
        /// </summary>
        public string? AfterScript { get; set; } = null;
    }
}
