namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Executor;
    using Redpoint.OpenGE.Executor.TaskExecutors;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    internal interface ITaskDescriptorFactory
    {
        int ScoreTaskSpec(GraphTaskSpec spec);

        ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(GraphTaskSpec spec);
    }

    internal class RemoteMsvcClTaskDescriptorFactory : ITaskDescriptorFactory
    {
        private readonly ILogger<RemoteMsvcClTaskDescriptorFactory> _logger;
        private readonly LocalTaskDescriptorFactory _localTaskDescriptorFactory;

        public RemoteMsvcClTaskDescriptorFactory(
            ILogger<RemoteMsvcClTaskDescriptorFactory> logger,
            LocalTaskDescriptorFactory localTaskDescriptorFactory)
        {
            _logger = logger;
            _localTaskDescriptorFactory = localTaskDescriptorFactory;
        }

        public int ScoreTaskSpec(GraphTaskSpec spec)
        {
            if (Path.GetFileName(spec.Tool.Path) == "cl.exe" &&
                spec.Arguments.Length > 0 &&
                spec.Arguments[0].StartsWith('@'))
            {
                return 1000;
            }

            return -1;
        }

        public async ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(GraphTaskSpec spec)
        {
            // Get the response file path, removing the leading '@'.
            var responseFilePath = spec.Arguments[0].Substring(1);

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
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
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
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    var info = new DirectoryInfo(path);
                    if (info.Exists)
                    {
                        includeDirectories.Add(info);
                    }
                }
                else if (line.StartsWith("/FI"))
                {
                    var path = line.Substring("/FI".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
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
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    pchInputFile = new FileInfo(path);
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = line.Substring("/Yc".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
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
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    pchCacheFile = new FileInfo(path);
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = line.Substring("/Fo".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    outputPath = new FileInfo(path);
                }
            }

            // Check we've got a valid configuration.
            if (inputFile == null || outputPath == null)
            {
                // Delegate to the local executor.
                _logger.LogWarning($"Forcing to local executor, input: {inputFile}, output: {outputPath}");
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec);
            }
        }
    }
}
