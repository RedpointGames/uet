namespace Redpoint.Uet.Core.BugReport
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class BugReportCollector : IDisposable
    {
        private readonly string? _zipPath;
        private readonly ZipArchive? _zipArchive;
        private readonly string _logPath;
        private readonly bool _storeInSupplemental;
        private readonly string _supplementalDirectory;
        private readonly StreamWriter _logStream;
        private readonly SemaphoreSlim _logMutex;
        private readonly SemaphoreSlim? _zipMutex;
        private readonly BugReportLoggerProvider _loggerProvider;
        private static Lazy<BugReportCollector> _instance = new Lazy<BugReportCollector>(() => new BugReportCollector(), LazyThreadSafetyMode.ExecutionAndPublication);
        private bool _disposedValue;

        private BugReportCollector()
        {
            var now = DateTimeOffset.Now;
            var supplementalDirectory = Environment.GetEnvironmentVariable("UET_BUG_REPORT_SUPPLEMENTAL_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(supplementalDirectory))
            {
                Directory.CreateDirectory(Path.Combine(supplementalDirectory, $"ChildProcess-{Environment.ProcessId}"));
                _logPath = Path.Combine(supplementalDirectory, $"ChildProcess-{Environment.ProcessId}", "uet.log");
                _storeInSupplemental = true;
                _supplementalDirectory = supplementalDirectory;
            }
            else
            {
                _zipPath = Path.Combine(Environment.CurrentDirectory, $"UET-BugReport-{now:yyyy.MM.dd.HH.mm.ss}.zip");
                _zipArchive = ZipFile.Open(_zipPath, ZipArchiveMode.Create);
                _logPath = Path.Combine(Environment.CurrentDirectory, $"UET-BugReport-{now:yyyy.MM.dd.HH.mm.ss}.log");
                _supplementalDirectory = Path.Combine(Environment.CurrentDirectory, $"UET-BugReport-{now:yyyy.MM.dd.HH.mm.ss}");
                Directory.CreateDirectory(_supplementalDirectory);
                _zipMutex = new SemaphoreSlim(1);
                _storeInSupplemental = false;
                Environment.SetEnvironmentVariable("UET_BUG_REPORT_SUPPLEMENTAL_DIRECTORY", _supplementalDirectory);
            }
            _logStream = new StreamWriter(new FileStream(_logPath, FileMode.Create, FileAccess.Write));
            _logMutex = new SemaphoreSlim(1);
            _loggerProvider = new BugReportLoggerProvider(this);
        }

        public static void DisposeIfInitialized()
        {
            if (_instance.IsValueCreated)
            {
                Instance.Dispose();
            }
        }

        public ILoggerProvider LoggerProvider => _loggerProvider;

        public static BugReportCollector Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        public void CollectFileForBugReport(string path, string filenameInZip)
        {
            if (_storeInSupplemental)
            {
                File.Copy(path, Path.Combine(_supplementalDirectory, $"ChildProcess-{Environment.ProcessId}", filenameInZip));
            }
            else
            {
                _zipMutex!.Wait();
                try
                {
                    _zipArchive!.CreateEntryFromFile(path, filenameInZip);
                }
                finally
                {
                    _zipMutex.Release();
                }
            }
        }

        private class BugReportLoggerProvider : ILoggerProvider
        {
            private readonly BugReportCollector _collector;

            public BugReportLoggerProvider(BugReportCollector collector)
            {
                _collector = collector;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new BugReportLogger(_collector, categoryName);
            }

            public void Dispose()
            {
            }
        }

        private class BugReportLogger : ILogger
        {
            private readonly BugReportCollector _collector;
            private readonly string _categoryName;

            public BugReportLogger(BugReportCollector collector, string categoryName)
            {
                _collector = collector;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var logLevelString = logLevel switch
                {
                    LogLevel.Trace => "trace",
                    LogLevel.Debug => "debug",
                    LogLevel.Information => "info",
                    LogLevel.Warning => "warn",
                    LogLevel.Error => "error",
                    LogLevel.Critical => "crit",
                    _ => ""
                };

                _collector._logMutex.Wait();
                try
                {
                    _collector._logStream.WriteLine($"[{DateTimeOffset.UtcNow:yyyy.MM.dd.HH.mm.ss.fffffff}] [{logLevelString}] [{_categoryName}] [{eventId}] {formatter(state, exception)}");
                }
                finally
                {
                    _collector._logMutex.Release();
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _loggerProvider.Dispose();

                    _logStream.Flush();
                    _logStream.Close();
                    _logStream.Dispose();

                    if (_storeInSupplemental)
                    {
                        // Nothing needed - parent process will add all supplemental files to ZIP.
                    }
                    else
                    {
                        _zipMutex!.Wait();
                        try
                        {
                            _zipArchive!.CreateEntryFromFile(_logPath, "uet.log");

                            foreach (var file in new DirectoryInfo(_supplementalDirectory).EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                _zipArchive!.CreateEntryFromFile(
                                    file.FullName,
                                    Path.GetRelativePath(_supplementalDirectory, file.FullName));
                            }
                        }
                        finally
                        {
                            File.Delete(_logPath);
                            try
                            {
                                Directory.Delete(_supplementalDirectory, true);
                            }
                            catch { }
                            _zipMutex.Release();
                        }
                        _zipArchive!.Dispose();

                        Console.WriteLine($"Bug report archive saved to: {_zipPath}");
                        Console.WriteLine("This file may contain personally identifiable data or project-specific information.");
                        Console.WriteLine("Please attach this file when opening an issue on: https://github.com/RedpointGames/uet/issues/new");
                    }

                    _logMutex.Dispose();
                    _zipMutex?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
