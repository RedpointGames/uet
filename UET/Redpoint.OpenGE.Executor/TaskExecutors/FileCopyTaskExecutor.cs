namespace Redpoint.OpenGE.Executor.TaskExecutors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor.BuildSetData;
    using System;
    using System.Threading.Tasks;

    internal class FileCopyTaskExecutor : IOpenGETaskExecutor
    {
        private readonly ILogger<FileCopyTaskExecutor> _logger;

        public FileCopyTaskExecutor(
            ILogger<FileCopyTaskExecutor> logger)
        {
            _logger = logger;
        }

        public int ScoreTask(
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments)
        {
            if (Path.GetFileName(tool.Path).Equals("cmd.exe", StringComparison.InvariantCultureIgnoreCase) &&
                arguments.Length == 4 &&
                arguments[0] == "/c" &&
                arguments[1] == "copy")
            {
                // We really want to handle this.
                return 10000;
            }
            else
            {
                // We can't handle anything else.
                return -1;
            }
        }

        private class NullCore : IDisposable
        {
            public void Dispose()
            {
            }

            public override string ToString()
            {
                return "local machine";
            }
        }

        public Task<int> ExecuteTaskAsync(
            IDisposable virtualCore,
            string buildStatusLogPrefix,
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments,
            Dictionary<string, string> globalEnvironmentVariables,
            Action<string> onStandardOutputLine,
            Action<string> onStandardErrorLine,
            CancellationToken cancellationToken)
        {
            // @note: Use a workaround for copying files because re-invoking cmd.exe for the copies
            // seems a little jank (at least for one confidential platform). Detect file copying tasks
            // and just do it from C# instead.
            var from = arguments[2].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var to = arguments[3].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            try
            {
                _logger.LogInformation($"{buildStatusLogPrefix} Copying '{from}' to '{to}' via OpenGE...");
                File.Copy(from, to);
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{buildStatusLogPrefix} Failed to copy '{from}' to '{to}' via OpenGE: {ex.Message}");
                return Task.FromResult(1);
            }
        }

        public Task<IDisposable> AllocateVirtualCoreForTaskExecutionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IDisposable>(new NullCore());
        }
    }
}
