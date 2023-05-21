namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    public enum BuildServerJobPlatform
    {
        /// <summary>
        /// Execute on Windows.
        /// </summary>
        Windows,

        /// <summary>
        /// Execute on macOS.
        /// </summary>
        Mac,

        /// <summary>
        /// These build jobs don't actually execute and your
        /// build server implementation should not generate
        /// jobs for them.
        /// </summary>
        Meta
    }
}
