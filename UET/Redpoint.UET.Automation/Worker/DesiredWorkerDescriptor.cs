namespace Redpoint.UET.Automation.Worker
{
    public class DesiredWorkerDescriptor
    {
        /// <summary>
        /// The platform to launch for.
        /// </summary>
        public required string Platform { get; set; }

        /// <summary>
        /// If true, this worker should be launched using the editor binaries instead of a packaged game.
        /// </summary>
        public required bool IsEditor { get; set; }

        /// <summary>
        /// The configuration to launch for (e.g. Development).
        /// </summary>
        public required string Configuration { get; set; }

        /// <summary>
        /// The target to launch (e.g. UnrealEditor or ProjectEditor).
        /// </summary>
        public required string Target { get; set; }

        /// <summary>
        /// The path to the .uproject file.
        /// </summary>
        public required string UProjectPath { get; set; }

        /// <summary>
        /// The path to the engine.
        /// </summary>
        public required string EnginePath { get; set; }

        /// <summary>
        /// The minimum number of workers to launch of this type. If there are fewer devices than this number, the device count is used as the minimum instead, with the exception that if there are no devices then the automation run fails (because we can't run the tests at all vs. them just running more slowly than expected).
        /// </summary>
        public required int? MinWorkerCount { get; set; }

        /// <summary>
        /// The maximum number of workers to launch of this type. If not set, the worker pool will automatically set the target number of workers to match the amount of work that needs doing.
        /// </summary>
        public required int? MaxWorkerCount { get; set; }

        /// <summary>
        /// If set, rendering and audio will be enabled on the worker. Only relevant for workers running on a desktop platform. Defaults to false.
        /// </summary>
        public bool EnableRendering { get; set; }
    }
}
