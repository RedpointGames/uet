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
    using Redpoint.OpenGE.Core;
    using System.IO;

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

        private record class PotentiallyQuotedPath
        {
            private bool _quoted;
            private string _path;

            public PotentiallyQuotedPath(string potentiallyQuotedPath)
            {
                _quoted = potentiallyQuotedPath.StartsWith('"');
                _path = potentiallyQuotedPath.Trim('"');
            }

            public void MakeAbsolutePath(string workingDirectory)
            {
                var oldPath = _path;
                _path = System.IO.Path.IsPathRooted(_path) ? _path : System.IO.Path.Combine(workingDirectory, _path);
                if (!oldPath.Contains(" ") && _path.Contains(" "))
                {
                    // We just added a space into this path, so it probably needs to be quoted to get the right effect.
                    _quoted = true;
                }
            }

            public string Path
            {
                get
                {
                    return _path;
                }
                set
                {
                    _path = value;
                }
            }

            public string QuoteAndRemotifyTarget(FileSystemInfo info)
            {
                if (_quoted)
                {
                    return '"' + info.FullName.RemotifyPath() + '"';
                }
                return info.FullName.RemotifyPath()!;
            }

            public override string ToString()
            {
                if (_quoted)
                {
                    return '"' + _path + '"';
                }
                return _path;
            }
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
                    var path = new PotentiallyQuotedPath(line);
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    inputFile = new FileInfo(path.Path);
                    remotedResponseFileLines.Add(path.QuoteAndRemotifyTarget(inputFile));
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
                    var path = new PotentiallyQuotedPath(line.Substring(line.StartsWith("/I") ? "/I ".Length : "/external:I ".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    var info = new DirectoryInfo(path.Path);
                    if (info.Exists)
                    {
                        if (line.StartsWith("/I"))
                        {
                            includeDirectories.Add(info);
                            remotedResponseFileLines.Add("/I " + path.QuoteAndRemotifyTarget(info));
                        }
                        else
                        {
                            includeDirectories.Add(info);
                            remotedResponseFileLines.Add("/external:I " + path.QuoteAndRemotifyTarget(info));
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"'{path}' does not exist.");
                    }
                }
                else if (line.StartsWith("/FI"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/FI".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    var fi = new FileInfo(path.Path);
                    forceIncludeFiles.Add(fi);
                    remotedResponseFileLines.Add("/FI" + path.QuoteAndRemotifyTarget(fi));
                }
                else if (line.StartsWith("/Yu"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yu".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                    remotedResponseFileLines.Add("/Yu" + path.QuoteAndRemotifyTarget(pchInputFile));
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yc".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                    isCreatingPch = true;
                    remotedResponseFileLines.Add("/Yc" + path.QuoteAndRemotifyTarget(pchInputFile));
                }
                else if (line.StartsWith("/Fp"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fp".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchCacheFile = new FileInfo(path.Path);
                    remotedResponseFileLines.Add("/Fp" + path.QuoteAndRemotifyTarget(pchCacheFile));
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fo".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    outputPath = new FileInfo(path.Path);
                    remotedResponseFileLines.Add("/Fo" + path.QuoteAndRemotifyTarget(outputPath));
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
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            using (var reader = new StreamReader(inputFile.FullName))
            {
                var inputContent = await reader.ReadToEndAsync();
                var inputLines = inputContent.Replace("\r\n", "\n").Split('\n');

                // If any includes in the input file are absolute paths, then we need to remotify
                // the path and virtualise the content. This happens when Unreal is doing unity builds or
                // generating PCH files.
                var newInputLines = new List<string>();
                var isVirtualised = false;
                foreach (var line in inputLines)
                {
                    if (line.TrimStart().StartsWith("#include"))
                    {
                        string includePath;
                        var included = line.Substring("#include".Length).Trim();
                        var isSystem = false;
                        if (included.StartsWith('"'))
                        {
                            includePath = included.Trim('"');
                        }
                        else if (included.StartsWith('<'))
                        {
                            includePath = included.TrimStart('<').TrimEnd('>');
                            isSystem = true;
                        }
                        else
                        {
                            continue;
                        }
                        includePath = includePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                        if (Path.IsPathRooted(includePath))
                        {
                            if (!isSystem)
                            {
                                newInputLines.Add($"#include \"{includePath.RemotifyPath()!.Replace('\\', '/')}\"");
                            }
                            else
                            {
                                newInputLines.Add($"#include <{includePath.RemotifyPath()!.Replace('\\', '/')}>");
                            }
                            isVirtualised = true;
                        }
                        else
                        {
                            newInputLines.Add(line);
                        }
                    }
                    else
                    {
                        newInputLines.Add(line);
                    }
                }

                // Add the input file based on whether it's virtualised.
                if (isVirtualised)
                {
                    inputsByPathOrContent.AbsolutePathsToVirtualContent.Add(
                        inputFile.FullName,
                        string.Join('\n', newInputLines));
                    // @note: Remove the input file from dependent paths because we virtualise it instead.
                    dependentFiles.DependsOnPaths.Remove(inputFile.FullName);
                    _logger.LogInformation($"Virtualising: {inputFile.FullName}");
                }
                else
                {
                    inputsByPathOrContent.AbsolutePaths.Add(inputFile.FullName);
                    _logger.LogInformation($"Not virtualising: {inputFile.FullName}");
                }
            }
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

            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
