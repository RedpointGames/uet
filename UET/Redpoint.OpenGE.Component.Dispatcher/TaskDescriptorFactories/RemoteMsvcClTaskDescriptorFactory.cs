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

        private static readonly HashSet<string> _knownMachineSpecificEnvironmentVariables = new HashSet<string>
        {
            "ALLUSERSPROFILE",
            "APPDATA",
            "CommonProgramFiles",
            "CommonProgramFiles(x86)",
            "CommonProgramW6432",
            "COMPUTERNAME",
            "ComSpec",
            "DriverData",
            "HOMEDRIVE",
            "HOMEPATH",
            "LOCALAPPDATA",
            "LOGONSERVER",
            "NUMBER_OF_PROCESSORS",
            "OneDrive",
            "OS",
            "Path",                                 // @note: Should this really be excluded?
            "PATHEXT",
            "POWERSHELL_DISTRIBUTION_CHANNEL",
            "PROCESSOR_ARCHITECTURE",
            "PROCESSOR_IDENTIFIER",
            "PROCESSOR_LEVEL",
            "PROCESSOR_REVISION",
            "ProgramData",
            "ProgramFiles",
            "ProgramFiles(x86)",
            "ProgramW6432",
            "PROMPT",
            "PSModulePath",                         // @note: Should this really be excluded?
            "PUBLIC",
            "SESSIONNAME",
            "SystemDrive",                          // @note: Should this really be excluded?
            "SystemRoot",                           // @note: Should this really be excluded?
            "TEMP",
            "TMP",
            "USERDOMAIN",
            "USERDOMAIN_ROAMINGPROFILE",
            "USERNAME",
            "USERPROFILE",
            "windir",
            "WSLENV",
            "WT_PROFILE_ID",
            "WT_SESSION",
        };

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
                spec.Arguments[0].StartsWith('@') &&
                OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
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
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken)
        {
            // Get the response file path, removing the leading '@'.
            var responseFilePath = spec.Arguments[0].Substring(1);
            if (!Path.IsPathRooted(responseFilePath))
            {
                responseFilePath = Path.Combine(spec.WorkingDirectory, responseFilePath);
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
            FileInfo? sourceDependencies = null;

            // Read all the lines.
            foreach (var line in File.ReadAllLines(responseFilePath))
            {
                if (!line.StartsWith('/'))
                {
                    // This is the input file.
                    var path = new PotentiallyQuotedPath(line);
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    inputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/D"))
                {
                    var define = line.Substring("/D".Length).Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    globalDefinitions[define[0]] = define.Length >= 2 ? define[1] : "1";
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
                        }
                        else
                        {
                            includeDirectories.Add(info);
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
                }
                else if (line.StartsWith("/Yu"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yu".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Yc"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Yc".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchInputFile = new FileInfo(path.Path);
                    isCreatingPch = true;
                }
                else if (line.StartsWith("/Fp"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fp".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    pchCacheFile = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/Fo"))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/Fo".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    outputPath = new FileInfo(path.Path);
                }
                else if (line.StartsWith("/sourceDependencies "))
                {
                    var path = new PotentiallyQuotedPath(line.Substring("/sourceDependencies ".Length));
                    path.MakeAbsolutePath(spec.WorkingDirectory);
                    sourceDependencies = new FileInfo(path.Path);
                }
            }

            // Check we've got a valid configuration.
            if (inputFile == null || outputPath == null)
            {
                // Delegate to the local executor.
                _logger.LogWarning($"Forcing to local executor, input: {inputFile}, output: {outputPath}");
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, guaranteedToExecuteLocally, cancellationToken);
            }

            // Determine the dependent header files.
            var preprocessorCache = await _preprocessorCacheAccessor.GetPreprocessorCacheAsync();
            PreprocessorResolutionResultWithTimingMetadata dependentFiles;
            if (!guaranteedToExecuteLocally)
            {
                try
                {
                    dependentFiles = await preprocessorCache.GetResolvedDependenciesAsync(
                        inputFile.FullName,
                        forceIncludeFiles.Select(x => x.FullName).ToArray(),
                        includeDirectories.Select(x => x.FullName).ToArray(),
                        globalDefinitions,
                        spec.ExecutionEnvironment.BuildStartTicks,
                        cancellationToken);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
                {
                    _logger.LogWarning($"Unable to remote compile this file as the preprocessor cache reported an error while parsing headers: {ex.Status.Detail}");
                    return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(spec, guaranteedToExecuteLocally, cancellationToken);
                }
            }
            else
            {
                dependentFiles = new PreprocessorResolutionResultWithTimingMetadata();
            }

            // Compute the environment variables, excluding any environment variables we
            // know to be per-machine.
            var environmentVariables = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var kv in spec.ExecutionEnvironment.EnvironmentVariables)
            {
                environmentVariables[kv.Key] = kv.Value;
            }
            foreach (var kv in spec.Environment.Variables)
            {
                environmentVariables[kv.Key] = kv.Value;
            }
            foreach (var knownKey in _knownMachineSpecificEnvironmentVariables)
            {
                environmentVariables.Remove(knownKey);
            }

            // Return the remote task descriptor.
            var descriptor = new RemoteTaskDescriptor();
            descriptor.ToolLocalAbsolutePath = spec.Tool.Path;
            descriptor.Arguments.Add("@" + responseFilePath);
            descriptor.EnvironmentVariables.MergeFrom(environmentVariables);
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            descriptor.UseFastLocalExecution = guaranteedToExecuteLocally;
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            if (pchCacheFile != null && !isCreatingPch)
            {
                inputsByPathOrContent.AbsolutePaths.Add(pchCacheFile.FullName);
            }
            inputsByPathOrContent.AbsolutePaths.AddRange(dependentFiles.DependsOnPaths);
            inputsByPathOrContent.AbsolutePaths.Add(responseFilePath);
            inputsByPathOrContent.AbsolutePaths.Add(inputFile.FullName);
            descriptor.InputsByPathOrContent = inputsByPathOrContent;
            descriptor.OutputAbsolutePaths.Add(outputPath.FullName);
            if (sourceDependencies != null)
            {
                descriptor.OutputAbsolutePaths.Add(sourceDependencies.FullName);
            }
            if (pchCacheFile != null && isCreatingPch)
            {
                descriptor.OutputAbsolutePaths.Add(pchCacheFile.FullName);
            }

            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
