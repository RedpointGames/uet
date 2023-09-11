namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Commandlet
{
    using Redpoint.Uet.Configuration;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTestCommandlet
    {
        /// <summary>
        /// The name of the commandlet to run.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Additional command line arguments to pass to Unreal Editor when running the commandlet.
        /// </summary>
        [JsonPropertyName("AdditionalArguments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? AdditionalArguments { get; set; } = null;

        /// <summary>
        /// A list of platforms to execute this commandlet test on. A set of "Win64", "Mac" or both.
        /// </summary>
        [JsonPropertyName("Platforms"), JsonRequired]
        public BuildConfigHostPlatform[] Platforms { get; set; } = Array.Empty<BuildConfigHostPlatform>();

        /// <summary>
        /// To detect scenarios where the Unreal Editor stalls between startup and the commandlet
        /// running, you can make your commandlet emit a log when it starts and then set that log
        /// line here. If the editor doesn't start executing the commandlet code before <see cref="LogStartTimeoutMinutes"/> have elapsed, 
        /// </summary>
        [JsonPropertyName("LogStartSignal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LogStartSignal { get; set; } = null;

        /// <summary>
        /// If set, the editor must emit <see cref="LogStartSignal"/> before the number of minutes elapses. If it doesn't, the editor instance is killed and the commandlet test is attempted
        /// again, up to <see cref="TestAttemptCount"/>.
        /// </summary>
        [JsonPropertyName("LogStartTimeoutMinutes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LogStartTimeoutMinutes { get; set; } = null;

        /// <summary>
        /// The path to a PowerShell script to run before the commandlet starts. This can be used
        /// to prepare the environment for the commandlet to run in, such as terminating processes
        /// or writing data to the filesystem.
        /// 
        /// The pre-start script is passed the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -TestProjectPath "C:\Path\To\UProject\File.uproject"
        /// </summary>
        [JsonPropertyName("PreStartScriptPath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PreStartScriptPath { get; set; } = null;

        /// <summary>
        /// The path to a PowerShell script which can be used to validate that the commandlet
        /// performed the intended behaviour. This script is only run if the editor exits with
        /// a non-zero exit code, and should be used to validate some global state of the
        /// system (such that the commandlet ran a process, or modified files on the filesystem).
        /// 
        /// The validation script is passed the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -TestProjectPath "C:\Path\To\UProject\File.uproject"
        /// </summary>
        [JsonPropertyName("ValidationScriptPath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ValidationScriptPath { get; set; } = null;

        /// <summary>
        /// The maximum number of times this commandlet test should be attempted if it fails. If not set, the commandlet is never automatically retried.
        /// </summary>
        [JsonPropertyName("TestAttemptCount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TestAttemptCount { get; set; }

        /// <summary>
        /// If set, this global mutex is obtained for the duration of the test. This includes the
        /// pre-start and validation scripts. This can be used to ensure that a commandlet that
        /// impacts the global environment (like running a process) will not be run concurrently
        /// across different build jobs on the same machine.
        /// </summary>
        [JsonPropertyName("GlobalMutexName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalMutexName { get; set; } = null;
    }
}
