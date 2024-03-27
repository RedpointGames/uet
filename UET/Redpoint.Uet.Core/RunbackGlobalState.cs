namespace Redpoint.Uet.Core
{
    using Redpoint.Uet.CommonPaths;
    using System;

    public static class RunbackGlobalState
    {
        public static Guid RunbackId { get; } = Guid.NewGuid();

        public static string RunbackDirectoryPath { get; } = UetPaths.UetRunbackDirectoryPath;

        public static string RunbackPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.json");

        public static string RunbackLogPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.log");
    }
}
