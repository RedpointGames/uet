namespace Redpoint.OpenGE.Executor.TaskExecutors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor.BuildSetData;
    using Redpoint.ProcessExecution;
    using System;
    using System.Threading.Tasks;

    internal class LocalTaskExecutor : ITaskExecutor
    {
        private readonly ILogger<LocalTaskExecutor> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ICoreReservation _coreReservation;

        public LocalTaskExecutor(
            ILogger<LocalTaskExecutor> logger,
            IProcessExecutor processExecutor,
            ICoreReservation coreReservation)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _coreReservation = coreReservation;
        }

        public int ScoreTask(
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments)
        {
            // We can run anything locally, but we'd really like not to.
            return 0;
        }

        private class LocalCore : IDisposable
        {
            private readonly ICoreReservation _coreReservation;
            private readonly int _core;

            public LocalCore(ICoreReservation coreReservation, int core)
            {
                _coreReservation = coreReservation;
                _core = core;
            }

            public void Dispose()
            {
                _coreReservation.ReleaseCoreAsync(_core, CancellationToken.None);
            }

            public override string ToString()
            {
                return $"core {_core}";
            }
        }

        public async Task<IDisposable> AllocateVirtualCoreForTaskExecutionAsync(
            CancellationToken cancellationToken)
        {
            var core = await _coreReservation.AllocateCoreAsync(cancellationToken);
            return new LocalCore(_coreReservation, core);
        }

        public async Task<int> ExecuteTaskAsync(
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
            try
            {
                var localEnvironmentVariables = new Dictionary<string, string>(globalEnvironmentVariables);
                foreach (var kv in environment.Variables)
                {
                    localEnvironmentVariables[kv.Key] = kv.Value;
                }
                return await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = tool.Path,
                        Arguments = arguments,
                        EnvironmentVariables = localEnvironmentVariables,
                        WorkingDirectory = task.BuildSetTask.WorkingDir,
                    },
                    CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                    {
                        ReceiveStdout = (line) =>
                        {
                            onStandardOutputLine(line.Trim());
                            if (line.Trim() != task.BuildSetTask.Caption)
                            {
                                _logger.LogInformation($"{buildStatusLogPrefix} {line}");
                            }
                            return false;
                        },
                        ReceiveStderr = (line) =>
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                _logger.LogError($"{buildStatusLogPrefix} {line}");
                            }
                            else
                            {
                                // @note: On macOS, some output to standard error is just normal
                                // output and doesn't represent an error state.
                                _logger.LogInformation($"{buildStatusLogPrefix} {line}");
                            }
                            onStandardErrorLine(line.Trim());
                            return false;
                        }
                    }),
                    cancellationToken);
            }
            finally
            {
                virtualCore.Dispose();
            }
        }
    }
}
