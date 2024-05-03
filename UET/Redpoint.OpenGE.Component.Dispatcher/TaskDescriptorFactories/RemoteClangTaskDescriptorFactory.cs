namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Clang;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.ProcessExecution;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class RemoteClangTaskDescriptorFactory : RemoteTaskDescriptorFactory
    {
        private readonly ILogger<RemoteClangTaskDescriptorFactory> _logger;
        private readonly LocalTaskDescriptorFactory _localTaskDescriptorFactory;
        private readonly IMsvcResponseFileParser _msvcResponseFileParser;
        private readonly ICommonPlatformDefines _commonPlatformDefines;
        private readonly IProcessArgumentParser _processArgumentParser;

        public RemoteClangTaskDescriptorFactory(
            ILogger<RemoteClangTaskDescriptorFactory> logger,
            LocalTaskDescriptorFactory localTaskDescriptorFactory,
            IMsvcResponseFileParser msvcResponseFileParser,
            ICommonPlatformDefines commonPlatformDefines,
            IProcessArgumentParser processArgumentParser)
        {
            _logger = logger;
            _localTaskDescriptorFactory = localTaskDescriptorFactory;
            _msvcResponseFileParser = msvcResponseFileParser;
            _commonPlatformDefines = commonPlatformDefines;
            _processArgumentParser = processArgumentParser;
        }

        public override string PreparationOperationDescription => "parsing headers";

        public override string PreparationOperationCompletedDescription => "parsed headers";

        private readonly HashSet<string> _recognisedClangCompilers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "clang-cl.exe",
            "clang-tidy.exe"
        };

        public override int ScoreTaskSpec(GraphTaskSpec spec)
        {
            if (_recognisedClangCompilers.Contains(Path.GetFileName(spec.Tool.Path)))
            {
                if (spec.Arguments.Any(x => x.LogicalValue.StartsWith("-p=", StringComparison.Ordinal)) &&
                    spec.Arguments.Any(x => !x.LogicalValue.StartsWith('-') &&
                        (x.LogicalValue.EndsWith(".cpp", StringComparison.Ordinal) || x.LogicalValue.EndsWith(".c", StringComparison.Ordinal))))
                {
                    var compileCommandDatabase = spec.Arguments
                        .Where(x => x.LogicalValue.StartsWith("-p=", StringComparison.Ordinal))
                        .Select(x => x.LogicalValue.Split('=', 2)[1])
                        .First();
                    var inputFile = spec.Arguments
                        .Where(x => !x.LogicalValue.StartsWith('-') && (x.LogicalValue.EndsWith(".cpp", StringComparison.Ordinal) || x.LogicalValue.EndsWith(".c", StringComparison.Ordinal)))
                        .First();
                    if (File.Exists(compileCommandDatabase) &&
                        File.Exists(inputFile.LogicalValue))
                    {
                        return 1000;
                    }
                }
            }

            return -1;
        }

        public override async ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken)
        {
            ValueTask<TaskDescriptor> DelegateToLocalExecutor()
            {
                return _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(
                    spec,
                    guaranteedToExecuteLocally,
                    cancellationToken);
            }

            var luaScriptFiles = spec.Arguments
                .Where(x => x.LogicalValue.StartsWith("--lua-script-path=", StringComparison.Ordinal))
                .Select(x => x.LogicalValue.Split('=', 2)[1])
                .Select(x =>
                {
                    if (!Path.IsPathRooted(x))
                    {
                        return Path.Combine(spec.WorkingDirectory, x);
                    }
                    return x;
                })
                .ToArray();
            var compileCommandDatabase = spec.Arguments
                .Where(x => x.LogicalValue.StartsWith("-p=", StringComparison.Ordinal))
                .Select(x => x.LogicalValue.Split('=', 2)[1])
                .Select(x =>
                {
                    if (!Path.IsPathRooted(x))
                    {
                        return Path.Combine(spec.WorkingDirectory, x);
                    }
                    return x;
                })
                .First();
            var inputFile = spec.Arguments
                .Where(x => !x.LogicalValue.StartsWith('-') && (x.LogicalValue.EndsWith(".cpp", StringComparison.Ordinal) || x.LogicalValue.EndsWith(".c", StringComparison.Ordinal)))
                .Select(x =>
                {
                    if (!Path.IsPathRooted(x.LogicalValue))
                    {
                        return Path.Combine(spec.WorkingDirectory, x.LogicalValue);
                    }
                    return x.LogicalValue;
                })
                .First();
            var touchPathFile = spec.Arguments
                .Where(x => x.LogicalValue.StartsWith("--touch-path=", StringComparison.Ordinal))
                .Select(x => x.LogicalValue.Split('=', 2)[1])
                .Select(x =>
                {
                    if (!Path.IsPathRooted(x))
                    {
                        return Path.Combine(spec.WorkingDirectory, x);
                    }
                    return x;
                })
                .FirstOrDefault();

            ClangCompileCommand compileCommand;
            try
            {
                var compileCommands = JsonSerializer.Deserialize(
                    File.ReadAllText(compileCommandDatabase).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("'", "\"", StringComparison.Ordinal),
                    ClangCompileCommandJsonSerializerContext.Default.ClangCompileCommandArray);
                compileCommand = compileCommands!.First(x => string.Equals(x.File, inputFile, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await DelegateToLocalExecutor().ConfigureAwait(false);
            }

            if (compileCommand.Command == null)
            {
                return await DelegateToLocalExecutor().ConfigureAwait(false);
            }
            var commandArguments = _processArgumentParser.SplitArguments(compileCommand.Command!);
            var responseFile = commandArguments.FirstOrDefault(x => x.LogicalValue.StartsWith('@'))?.LogicalValue;
            if (responseFile == null)
            {
                return await DelegateToLocalExecutor().ConfigureAwait(false);
            }

            // Set up the compiler architype.
            var compilerArchitype = new CompilerArchitype
            {
                Clang = new ClangCompiler
                {
                    CppLanguageVersion = 201703 /* C++ 17 */,
                    CLanguageVersion = 201710 /* C 17 */,
                    MajorVersion = 15,
                    MinorVersion = 0,
                    PatchVersion = 7,
                    EmulatedMsvcVersion = 1935 /* 2022 17.5 */,
                    EmulatedMsvcFullVersion = 193599999 /* Latest 2022 17.5 */,
                }
            };
            _commonPlatformDefines.ApplyDefines("Win64", compilerArchitype);
            foreach (var arg in commandArguments.Where(x => x.LogicalValue.StartsWith("/D", StringComparison.Ordinal)))
            {
                var define = arg.LogicalValue[2..].Split('=', 2);
                if (define.Length == 1)
                {
                    compilerArchitype.TargetPlatformNumericDefines.Add(define[0], 1);
                }
                else if (long.TryParse(define[1], out var num))
                {
                    compilerArchitype.TargetPlatformNumericDefines.Add(define[0], 1);
                }
                else
                {
                    compilerArchitype.TargetPlatformStringDefines.Add(define[0], define[1]);
                }
            }

            // If we are running clang-tidy, then we're running an analyzer.
            if (Path.GetFileNameWithoutExtension(spec.Tool.Path) == "clang-tidy")
            {
                compilerArchitype.TargetPlatformNumericDefines.Add("__clang_analyzer__", 1);
            }

            // Parse the response file.
            var msvcParsedResponseFile = await _msvcResponseFileParser.ParseResponseFileAsync(
                responseFile[1..],
                spec.WorkingDirectory,
                guaranteedToExecuteLocally,
                spec.ExecutionEnvironment.BuildStartTicks,
                compilerArchitype,
                cancellationToken).ConfigureAwait(false);
            if (msvcParsedResponseFile == null)
            {
                return await DelegateToLocalExecutor().ConfigureAwait(false);
            }

            // Compute the environment variables, excluding any environment variables we
            // know to be per-machine.
            var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            // On Clang, when we're using a PCH file, we need to include the .cpp file next to the .h, since the
            // produced AST in the compiled PCH file implicitly depends on the .cpp file, even though the only thing
            // it contains is a .h include.
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            if (!msvcParsedResponseFile.IsCreatingPch && msvcParsedResponseFile.PchOriginalHeaderFile != null)
            {
                var relatedCppFile = Path.Combine(
                    msvcParsedResponseFile.PchOriginalHeaderFile.DirectoryName!,
                    Path.GetFileNameWithoutExtension(msvcParsedResponseFile.PchOriginalHeaderFile.Name) + ".cpp");
                if (File.Exists(relatedCppFile))
                {
                    inputsByPathOrContent.AbsolutePaths.Add(relatedCppFile);
                }
            }

            // Return the remote task descriptor.
            var descriptor = new RemoteTaskDescriptor();
            descriptor.ToolLocalAbsolutePath = spec.Tool.Path;
            descriptor.Arguments.AddRange(spec.Arguments.Select(x => new ProcessArgument
            {
                LogicalValue = x.LogicalValue,
                OriginalValue = x.OriginalValue,
            }));
            descriptor.EnvironmentVariables.MergeFrom(environmentVariables);
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            descriptor.UseFastLocalExecution = guaranteedToExecuteLocally;
            if (msvcParsedResponseFile.PchCacheFile != null && !msvcParsedResponseFile.IsCreatingPch)
            {
                inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }
            inputsByPathOrContent.AbsolutePaths.AddRange(msvcParsedResponseFile.DependentFiles.DependsOnPaths);
            inputsByPathOrContent.AbsolutePaths.AddRange(luaScriptFiles);
            inputsByPathOrContent.AbsolutePaths.Add(compileCommandDatabase);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.ResponseFilePath);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.InputFile.FullName);
            descriptor.TransferringStorageLayer = new TransferringStorageLayer();
            descriptor.TransferringStorageLayer.InputsByPathOrContent = inputsByPathOrContent;
            descriptor.TransferringStorageLayer.OutputAbsolutePaths.Add(msvcParsedResponseFile.OutputFile.FullName);
            if (msvcParsedResponseFile.SourceDependencies != null)
            {
                descriptor.TransferringStorageLayer.OutputAbsolutePaths.Add(msvcParsedResponseFile.SourceDependencies.FullName);
            }
            if (msvcParsedResponseFile.ClangDepfile != null)
            {
                descriptor.TransferringStorageLayer.OutputAbsolutePaths.Add(msvcParsedResponseFile.ClangDepfile.FullName);
            }
            if (msvcParsedResponseFile.PchCacheFile != null && msvcParsedResponseFile.IsCreatingPch)
            {
                descriptor.TransferringStorageLayer.OutputAbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }
            if (touchPathFile != null)
            {
                descriptor.TransferringStorageLayer.OutputAbsolutePaths.Add(touchPathFile);
            }

            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
