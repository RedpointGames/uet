namespace Redpoint.OpenGE.Core
{
    public static class WorkerDiscoveryConstants
    {
        public const int OpenGEProtocolVersion = 1;
        public static string OpenGEPlatformIdentifier => true switch
        {
            var v when v == OperatingSystem.IsWindows() => "win",
            var v when v == OperatingSystem.IsMacOS() => "mac",
            var v when v == OperatingSystem.IsLinux() => "lin",
            _ => "ukn",
        };
    }
}
