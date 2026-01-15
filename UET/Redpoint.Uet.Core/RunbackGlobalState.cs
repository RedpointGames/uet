namespace Redpoint.Uet.Core
{
    using Redpoint.Uet.CommonPaths;
    using System;
    using System.Diagnostics;

    public static class RunbackGlobalState
    {
        private static FileStream? _runbackFileStream;
        private static StackTrace? _runbackInitialOpenStackTrace;

        public static Guid RunbackId { get; } = Guid.NewGuid();

        public static string RunbackDirectoryPath { get; } = UetPaths.UetRunbackDirectoryPath;

        public static string RunbackPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.json");

        public static string RunbackLogPath { get; } = Path.Combine(RunbackDirectoryPath, $"{RunbackId}.log");

        public static FileStream RunbackFileStream
        {
            get
            {
                if (_runbackFileStream == null)
                {
                    _runbackFileStream = new FileStream(RunbackGlobalState.RunbackLogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
                    if (Environment.GetEnvironmentVariable("UET_TRACE") == "1")
                    {
                        _runbackInitialOpenStackTrace = new StackTrace();
                    }
                }
                else
                {
                    if (Environment.GetEnvironmentVariable("UET_TRACE") == "1")
                    {
                        Console.WriteLine("=== BUG: Runback file stream is being opened twice ===");
                        Console.WriteLine();
                        Console.WriteLine("FIRST OPEN WAS AT: ");
                        if (_runbackInitialOpenStackTrace != null)
                        {
                            Console.WriteLine(_runbackInitialOpenStackTrace.ToString());
                        }
                        else
                        {
                            Console.WriteLine("(unknown)");
                        }
                        Console.WriteLine();
                        Console.WriteLine("CURRENT OPEN IS AT: ");
                        Console.WriteLine(new StackTrace().ToString());
                        Console.WriteLine();
                        Console.WriteLine("======================================================");
                    }
                }

                return _runbackFileStream;
            }
        }
    }
}
