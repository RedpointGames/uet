namespace Redpoint.Uet.Automation.TestLogger
{
    public static class AutomationLoggerPipe
    {
        /// <summary>
        /// If set, the "logging over gRPC" pipe can be used. By default, this is set to false if
        /// UET_AUTOMATION_NO_LOGGER_PIPE environment variable is set, but the UET startup also turns off
        /// the logger pipe if it looks like we're invoking the 'git-credential-helper' command.
        /// </summary>
        public static bool AllowLoggerPipe { get; set; } = Environment.GetEnvironmentVariable("UET_AUTOMATION_NO_LOGGER_PIPE") != "1";
    }
}
