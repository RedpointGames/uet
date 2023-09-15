namespace Redpoint.Uet.Core
{
    using System;

    public static class RunbackGlobalState
    {
        public static Guid RunbackId { get; } = Guid.NewGuid();

        public static string RunbackDirectoryPath { get; } = true switch
        {
            var v when v == OperatingSystem.IsWindows() => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "UET",
                "Runbacks"),
            var v when v == OperatingSystem.IsMacOS() => Path.Combine(
                "/Users/Shared",
                "UET",
                "Runbacks"),
            _ => throw new PlatformNotSupportedException("This platform is not supported for runbacks.")
        };

        public static string RunbackPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.json");

        public static string RunbackLogPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.log");
    }
}
