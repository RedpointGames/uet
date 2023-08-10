namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Grpc.Core;

    internal class RemoteMsvcClTaskDescriptorFactory : ITaskDescriptorFactory
    {
        private readonly ILogger<RemoteMsvcClTaskDescriptorFactory> _logger;
        private readonly LocalTaskDescriptorFactory _localTaskDescriptorFactory;
        private readonly IPreprocessorCacheAccessor _preprocessorCacheAccessor;

        public RemoteMsvcClTaskDescriptorFactory(
            ILogger<RemoteMsvcClTaskDescriptorFactory> logger,
            LocalTaskDescriptorFactory localTaskDescriptorFactory,
            IPreprocessorCacheAccessor preprocessorCacheAccessor)
        {
            _logger = logger;
            _localTaskDescriptorFactory = localTaskDescriptorFactory;
            _preprocessorCacheAccessor = preprocessorCacheAccessor;
        }

        public string PreparationOperationDescription => "parsing headers";

        public string PreparationOperationCompletedDescription => "parsed headers";

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

        public async ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            CancellationToken cancellationToken)
        {
            // Get the response file path, removing the leading '@'.
            var responseFilePath = spec.Arguments[0].Substring(1);
            if (!Path.IsPathRooted(responseFilePath))
            {
                responseFilePath = Path.Combine(spec.WorkingDirectory, responseFilePath);
            }
            var responseFileRemotePath = responseFilePath.RemotifyPath();
            if (responseFileRemotePath == null)
            {
                // This response file can't be remoted.
                _logger.LogWarning($"Forcing to local executor because the response file isn't at a remotable path: '{responseFilePath}'");
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, cancellationToken);
            }

            // Store the data that we need to figure out how to remote this.
            FileInfo? inputFile = null;
            var includeDirectories = new List<DirectoryInfo>();
            var forceIncludeFiles = new List<FileInfo>();
            var globalDefinitions = new Dictionary<string, string>();
            var isCreatingPch = false;
            FileInfo? pchInputFile = null;
            FileInfo? pchCacheFile = null;
            FileInfo? outputPath = null;
            List<string> remotedResponseFileLines = new List<string>();

            // Read all the lines.
            foreach (var line in File.ReadAllLines(responseFilePath))
            {
                if (!line.StartsWith('/'))
                {
                    // This is the input file.
                    var path = line;
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    inputFile = new FileInfo(path);
                    remotedResponseFileLines.Add(inputFile.FullName.RemotifyPath()!);
                }
                else if (line.StartsWith("/D"))
                {
                    var define = line.Substring("/D".Length).Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    globalDefinitions[define[0]] = define.Length >= 2 ? define[1] : "1";
                    remotedResponseFileLines.Add(line);
                }
                else if (line.StartsWith("/I") ||
                    line.StartsWith("/external:I"))
                {
                    var path = line.Substring(line.StartsWith("/I") ? "/I ".Length : "/external:I ".Length);
                    path = path.Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    var info = new DirectoryInfo(path);
                    if (info.Exists)
                    {
                        if (line.StartsWith("/I"))
                        {
                            includeDirectories.Add(info);
                            remotedResponseFileLines.Add("/I " + info.FullName.RemotifyPath()!);
                        }
                        else
                        {
                            includeDirectories.Add(info);
                            remotedResponseFileLines.Add("/external:I " + info.FullName.RemotifyPath()!);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"'{path}' does not exist.");
                    }
                }
                else if (line.StartsWith("/FI"))
                {
                    var path = line.Substring("/FI".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    var fi = new FileInfo(path);
                    forceIncludeFiles.Add(fi);
                    remotedResponseFileLines.Add("/FI" + fi.FullName.RemotifyPath()!);
                }
                else if (line.StartsWith("/Yu"))
                {
                    var path = line.Substring("/Yu".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    pchInputFile = new FileInfo(path);
                    remotedResponseFileLines.Add("/Yu" + pchInputFile.FullName.RemotifyPath()!);
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = line.Substring("/Yc".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    pchInputFile = new FileInfo(path);
                    isCreatingPch = true;
                    remotedResponseFileLines.Add("/Yc" + pchInputFile.FullName.RemotifyPath()!);
                }
                else if (line.StartsWith("/Fp"))
                {
                    var path = line.Substring("/Fp".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    pchCacheFile = new FileInfo(path);
                    remotedResponseFileLines.Add("/Fp" + pchCacheFile.FullName.RemotifyPath()!);
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = line.Substring("/Fo".Length).Trim('"');
                    path = Path.IsPathRooted(path) ? path : Path.Combine(spec.WorkingDirectory, path);
                    outputPath = new FileInfo(path);
                    remotedResponseFileLines.Add("/Fo" + outputPath.FullName.RemotifyPath()!);
                }
                else
                {
                    remotedResponseFileLines.Add(line);
                }
            }

            // Check we've got a valid configuration.
            if (inputFile == null || outputPath == null)
            {
                // Delegate to the local executor.
                _logger.LogWarning($"Forcing to local executor, input: {inputFile}, output: {outputPath}");
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, cancellationToken);
            }

            // Determine the dependent header files.
            var preprocessorCache = await _preprocessorCacheAccessor.GetPreprocessorCacheAsync();
            PreprocessorResolutionResultWithTimingMetadata dependentFiles;
            try
            {
                dependentFiles = await preprocessorCache.GetResolvedDependenciesAsync(
                    inputFile.FullName,
                    pchInputFile != null && forceIncludeFiles.Any(x => x.FullName == pchInputFile.FullName)
                        ? new[] { pchInputFile.FullName }
                        : Array.Empty<string>(),
                    forceIncludeFiles.Select(x => x.FullName).Where(x => x != pchInputFile?.FullName).ToArray(),
                    includeDirectories.Select(x => x.FullName).ToArray(),
                    globalDefinitions,
                    spec.ExecutionEnvironment.BuildStartTicks,
                    cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogWarning($"Unable to remote compile this file as the preprocessor cache reported an error while parsing headers: {ex.Status.Detail}");
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, cancellationToken);
            }

            // Return the remote task descriptor.
            var descriptor = new RemoteTaskDescriptor();
            descriptor.ToolLocalAbsolutePath = spec.Tool.Path;
            descriptor.Arguments.Add("@" + responseFileRemotePath);
            descriptor.EnvironmentVariables.MergeFrom(spec.ExecutionEnvironment.EnvironmentVariables);
            descriptor.EnvironmentVariables.MergeFrom(spec.Environment.Variables);
            descriptor.WorkingDirectory = spec.WorkingDirectory;
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            inputsByPathOrContent.AbsolutePaths.Add(inputFile.FullName);
            if (pchCacheFile != null && !isCreatingPch)
            {
                inputsByPathOrContent.AbsolutePaths.Add(pchCacheFile.FullName);
            }
            inputsByPathOrContent.AbsolutePaths.AddRange(dependentFiles.DependsOnPaths);
            inputsByPathOrContent.AbsolutePathsToVirtualContent.Add(
                responseFilePath,
                string.Join('\n', remotedResponseFileLines));
            descriptor.InputsByPathOrContent = inputsByPathOrContent;
            descriptor.OutputAbsolutePaths.Add(outputPath.FullName);
            if (pchCacheFile != null && isCreatingPch)
            {
                descriptor.OutputAbsolutePaths.Add(pchCacheFile.FullName);
            }

            //return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, cancellationToken);
            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
