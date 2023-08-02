namespace Redpoint.OpenGE.Executor.TaskExecutors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor.BuildSetData;
    using Redpoint.OpenGE.Executor.CompilerDb;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RemoteMsvcClTaskExecutor : ITaskExecutor
    {
        private readonly ILogger<RemoteMsvcClTaskExecutor> _logger;
        private readonly ICompilerDb _compilerDb;
        private readonly LocalTaskExecutor _localTaskExecutor;

        public RemoteMsvcClTaskExecutor(
            ILogger<RemoteMsvcClTaskExecutor> logger,
            ICompilerDb compilerDb,
            LocalTaskExecutor localTaskExecutor)
        {
            _logger = logger;
            _compilerDb = compilerDb;
            _localTaskExecutor = localTaskExecutor;
        }

        public int ScoreTask(
            OpenGETask task,
            BuildSetEnvironment environment,
            BuildSetTool tool,
            string[] arguments)
        {
        }

        public Task<IDisposable> AllocateVirtualCoreForTaskExecutionAsync(
            CancellationToken cancellationToken)
        {
            return _localTaskExecutor.AllocateVirtualCoreForTaskExecutionAsync(cancellationToken);
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

            var stopwatch = Stopwatch.StartNew();

            // Build the dependent files list from the list of headers
            // reachable from the input file.
            var dependentFiles = new HashSet<string>(
                await _compilerDb.ProcessRootFileAsync(
                    inputFile.FullName,
                    includeDirectories,
                    systemIncludeDirectories,
                    globalDefinitions,
                    cancellationToken));

            // Remove headers from the dependent list if they're included in
            // the PCH file.
            if (pchInputFile != null && !isCreatingPch)
            {
                foreach (var file in await _compilerDb.ProcessRootFileAsync(
                    pchInputFile.FullName,
                    includeDirectories,
                    systemIncludeDirectories,
                    globalDefinitions,
                    cancellationToken))
                {
                    dependentFiles.Remove(file);
                }
            }

            // Add the PCH caching files to the headers.
            if (pchInputFile != null)
            {
                if (isCreatingPch)
                {
                    dependentFiles.Add(pchInputFile.FullName);
                }
                else if (pchCacheFile != null)
                {
                    dependentFiles.Add(pchCacheFile.FullName);
                }
            }

            _logger.LogInformation($"Transfer set of {dependentFiles.Count} computed in {stopwatch.Elapsed.TotalSeconds} seconds.");

            return await _localTaskExecutor.ExecuteTaskAsync(
                virtualCore,
                buildStatusLogPrefix,
                task,
                environment,
                tool,
                arguments,
                globalEnvironmentVariables,
                onStandardOutputLine,
                onStandardErrorLine,
                cancellationToken);
        }
    }
}
