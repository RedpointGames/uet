namespace AutomationRunner
{
    using System;

    public class ConsoleTestLogger : ITestLogger
    {
        private readonly bool _displayStartingMessages;
        private readonly bool _displayFullLogs;

        private string _colorRed = "\x1b[31m";
        private string _colorGreen = "\x1b[32m";
        private string _colorBlue = "\x1b[34m";
        private string _colorCyan = "\x1b[36m";
        private string _colorYellow = "\x1b[33m";
        private string _colorReset = "\x1b[0m";

        public ConsoleTestLogger(
            bool displayStartingMessages,
            bool displayFullLogs)
        {
            _displayStartingMessages = displayStartingMessages;
            _displayFullLogs = displayFullLogs;
        }

        private string Worker(Worker? worker)
        {
            if (worker == null)
            {
                return "[        ]";
            }
            return $"[Worker {worker.WorkerNum}]";
        }

        public void LogStartup(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [Startup] {message}");
        }

        public void LogTrace(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [Trace  ] {message}");
        }

        public void LogInformation(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [Info   ] {message}");
        }

        public void LogWarning(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [Warning] {message}");
        }

        public void LogError(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorRed}Error  {_colorReset}] {message}");
        }

        public void LogCrash(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorRed}Crash  {_colorReset}] {message}");
        }

        public void LogCallstack(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorRed}Callstk{_colorReset}] {message}");
        }

        public void LogException(Worker? worker, Exception exception, string context)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorRed}Error  {_colorReset}] {context}: ({exception.GetType().FullName}) {exception.Message}");
            Console.WriteLine(exception.StackTrace);
        }

        public void LogStderr(Worker worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [Stderr ] {message}");
        }

        public void LogStdout(Worker worker, string message)
        {
            if (_displayFullLogs)
            {
                Console.WriteLine($"{Worker(worker)} [Stdout ] {message}");
            }
        }

        public void LogDiscovered(Worker? worker, TestResult testResult)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorCyan}Listing{_colorReset}] {testResult.FullTestPath}");
        }

        public void LogStarted(Worker? worker, TestResult testResult)
        {
            if (_displayStartingMessages)
            {
                Console.WriteLine($"{Worker(worker)} [{_colorBlue}Started{_colorReset}] {testResult.FullTestPath}");
            }
        }

        public void LogWaiting(Worker? worker, string message)
        {
            Console.WriteLine($"{Worker(worker)} [{_colorYellow}Waiting{_colorReset}] {message}");
        }

        public void LogFinished(Worker? worker, TestResult testResult)
        {
            if (testResult.State == TestState.Success)
            {
                Console.WriteLine($"{Worker(worker)} [{_colorGreen}Success{_colorReset}] {testResult.FullTestPath} ({testResult.Duration:0.##} secs)");
            }
            // Don't emit for crashes; they will already have an entry emitted.
            else if (testResult.State != TestState.Crash)
            {
                Console.WriteLine($"{Worker(worker)} [{_colorRed}Failure{_colorReset}] {testResult.FullTestPath} ({testResult.Duration:0.##} secs)");
            }
        }
    }
}
