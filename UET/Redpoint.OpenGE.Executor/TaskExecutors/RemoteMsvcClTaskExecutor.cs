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

    internal class RemoteMsvcClTaskExecutor : IOpenGETaskExecutor
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
            if (Path.GetFileName(tool.Path) == "cl.exe" &&
                arguments.Length > 0 &&
                arguments[0].StartsWith('@'))
            {
                return 1000;
            }

            return -1;
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
            // Get the response file path, removing the leading '@'.
            var responseFilePath = arguments[0].Substring(1);

            // Store the data that we need to figure out how to remote this.
            FileInfo? inputFile = null;
            var includeDirectories = new List<DirectoryInfo>();
            var systemIncludeDirectories = new List<DirectoryInfo>();
            var forceIncludeFiles = new List<FileInfo>();
            var globalDefinitions = new Dictionary<string, string>();
            var isCreatingPch = false;
            FileInfo? pchInputFile = null;
            FileInfo? pchCacheFile = null;
            FileInfo? outputPath = null;

            // Read all the lines.
            foreach (var line in File.ReadAllLines(responseFilePath))
            {
                if (!line.StartsWith('/'))
                {
                    // This is the input file.
                    var path = line;
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    inputFile = new FileInfo(path);
                }
                else if (line.StartsWith("/D"))
                {
                    var define = line.Substring("/D".Length).Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    globalDefinitions[define[0]] = define.Length >= 2 ? define[1] : "1";
                }
                else if (line.StartsWith("/I") ||
                    line.StartsWith("/external:I"))
                {
                    var path = line.Substring(line.StartsWith("/I") ? "/I ".Length : "/external:I ".Length);
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    var info = new DirectoryInfo(path);
                    if (info.Exists)
                    {
                        includeDirectories.Add(info);
                    }
                }
                else if (line.StartsWith("/FI"))
                {
                    var path = line.Substring("/FI".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    forceIncludeFiles.Add(new FileInfo(path));

                    // Also read the #define from this file, since this is where we find the platform define.
                    foreach (var defineLine in File.ReadAllLines(path))
                    {
                        var dl = defineLine.TrimStart();
                        if (dl.StartsWith("#define "))
                        {
                            var dlComponents = dl.Substring("#define ".Length).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            globalDefinitions[dlComponents[0]] = dlComponents.Length >= 2 ? dlComponents[1] : "1";
                        }
                    }
                }
                else if (line.StartsWith("/Yu"))
                {
                    var path = line.Substring("/Yu".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    pchInputFile = new FileInfo(path);
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = line.Substring("/Yc".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    pchInputFile = new FileInfo(path);
                    isCreatingPch = true;

                    // Also read the #define from this file, since this is where we find the platform define.
                    foreach (var defineLine in File.ReadAllLines(path))
                    {
                        var dl = defineLine.TrimStart();
                        if (dl.StartsWith("#define "))
                        {
                            var dlComponents = dl.Substring("#define ".Length).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            globalDefinitions[dlComponents[0]] = dlComponents.Length >= 2 ? dlComponents[1] : "1";
                        }
                    }
                }
                else if (line.StartsWith("/Fp"))
                {
                    var path = line.Substring("/Fp".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    pchCacheFile = new FileInfo(path);
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = line.Substring("/Fo".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(task.BuildSetTask.WorkingDir!, path);
                    outputPath = new FileInfo(path);
                }
            }

            // Check we've got a valid configuration.
            if (inputFile == null ||
                outputPath == null)
            {
                // Delegate to the local executor.
                _logger.LogWarning($"Forcing to local executor, input: {inputFile}, output: {outputPath}");
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
