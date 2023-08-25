namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Clang;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc;
    using Redpoint.OpenGE.Protocol;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class RemoteClangTaskDescriptorFactory : RemoteTaskDescriptorFactory
    {
        private readonly ILogger<RemoteClangTaskDescriptorFactory> _logger;
        private readonly LocalTaskDescriptorFactory _localTaskDescriptorFactory;
        private readonly IMsvcResponseFileParser _msvcResponseFileParser;

        public RemoteClangTaskDescriptorFactory(
            ILogger<RemoteClangTaskDescriptorFactory> logger,
            LocalTaskDescriptorFactory localTaskDescriptorFactory,
            IMsvcResponseFileParser msvcResponseFileParser)
        {
            _logger = logger;
            _localTaskDescriptorFactory = localTaskDescriptorFactory;
            _msvcResponseFileParser = msvcResponseFileParser;
        }

        public override string PreparationOperationDescription => "parsing headers";

        public override string PreparationOperationCompletedDescription => "parsed headers";

        private readonly IReadOnlySet<string> _recognisedClangCompilers = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            "clang-cl.exe",
            "clang-tidy.exe"
        };

        public override int ScoreTaskSpec(GraphTaskSpec spec)
        {
            if (_recognisedClangCompilers.Contains(Path.GetFileName(spec.Tool.Path)))
            {
                if (spec.Arguments.Any(x => x.StartsWith("-p=")) &&
                    spec.Arguments.Any(x => !x.StartsWith("-") &&
                        (x.EndsWith(".cpp") || x.EndsWith(".c"))))
                {
                    var compileCommandDatabase = spec.Arguments
                        .Where(x => x.StartsWith("-p="))
                        .Select(x => x.Split('=', 2)[1])
                        .First();
                    var inputFile = spec.Arguments
                        .Where(x => !x.StartsWith("-") && (x.EndsWith(".cpp") || x.EndsWith(".c")))
                        .First();
                    if (File.Exists(compileCommandDatabase) &&
                        File.Exists(inputFile))
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
                .Where(x => x.StartsWith("--lua-script-path="))
                .Select(x => x.Split('=', 2)[1])
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
                .Where(x => x.StartsWith("-p="))
                .Select(x => x.Split('=', 2)[1])
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
                .Where(x => !x.StartsWith("-") && (x.EndsWith(".cpp") || x.EndsWith(".c")))
                .Select(x =>
                {
                    if (!Path.IsPathRooted(x))
                    {
                        return Path.Combine(spec.WorkingDirectory, x);
                    }
                    return x;
                })
                .First();

            ClangCompileCommand compileCommand;
            try
            {
                var compileCommands = JsonSerializer.Deserialize(
                    File.ReadAllText(compileCommandDatabase).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\""),
                    ClangCompileCommandJsonSerializerContext.Default.ClangCompileCommandArray);
                compileCommand = compileCommands!.First(x => string.Equals(x.File, inputFile, StringComparison.InvariantCultureIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await DelegateToLocalExecutor();
            }

            if (compileCommand.Command == null)
            {
                return await DelegateToLocalExecutor();
            }
            var commandArguments = CommandLineArgumentSplitter.SplitArguments(compileCommand.Command!);
            var responseFile = commandArguments.FirstOrDefault(x => x.StartsWith('@'));
            if (responseFile == null)
            {
                return await DelegateToLocalExecutor();
            }

            // Parse the response file.
            var msvcParsedResponseFile = await _msvcResponseFileParser.ParseResponseFileAsync(
                responseFile.Substring(1),
                spec.WorkingDirectory,
                guaranteedToExecuteLocally,
                spec.ExecutionEnvironment.BuildStartTicks,
                new CompilerArchitype
                {
                    Clang = new ClangCompiler
                    {
                        MajorVersion = 15,
                        MinorVersion = 0,
                        PatchVersion = 7,
                    },
                    TargetPlatformNumericDefines =
                    {
                        { "__x86_64__", 1 },
                        { "_WIN32", 1 },
                        { "_WIN64", 1 },
                        { "_WIN32_WINNT", 0x0601 /* Windows 7 */ },
                        { "_WIN32_WINNT_WIN10_TH2", 0x0A01 },
                        { "_WIN32_WINNT_WIN10_RS1", 0x0A02 },
                        { "_WIN32_WINNT_WIN10_RS2", 0x0A03 },
                        { "_WIN32_WINNT_WIN10_RS3", 0x0A04 },
                        { "_WIN32_WINNT_WIN10_RS4", 0x0A05 },
                        { "_NT_TARGET_VERSION_WIN10_RS4", 0x0A05 },
                        { "_WIN32_WINNT_WIN10_RS5", 0x0A06 },
                        { "_M_X64", 1 },
                        { "_VCRT_COMPILER_PREPROCESSOR", 1 },
                        /*
                         * Some undocumented define that the Windows headers rely on 
                         * for Compiled Hybrid Portable Executable (CHPE) support, 
                         * which is for x86 binaries that include ARM code. This
                         * undocumented feature has since been replaced with ARM64EC,
                         * so we don't need to actually support this; we just need the
                         * define so the headers work.
                         */
                        { "_M_HYBRID", 0 },
                    }
                },
                cancellationToken);
            if (msvcParsedResponseFile == null)
            {
                return await DelegateToLocalExecutor();
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
            descriptor.Arguments.AddRange(spec.Arguments);
            descriptor.EnvironmentVariables.MergeFrom(environmentVariables);
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            descriptor.UseFastLocalExecution = guaranteedToExecuteLocally;
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            if (msvcParsedResponseFile.PchCacheFile != null && !msvcParsedResponseFile.IsCreatingPch)
            {
                inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }
            inputsByPathOrContent.AbsolutePaths.AddRange(msvcParsedResponseFile.DependentFiles.DependsOnPaths);
            inputsByPathOrContent.AbsolutePaths.AddRange(luaScriptFiles);
            inputsByPathOrContent.AbsolutePaths.Add(compileCommandDatabase);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.ResponseFilePath);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.InputFile.FullName);
            descriptor.InputsByPathOrContent = inputsByPathOrContent;
            descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.OutputFile.FullName);
            if (msvcParsedResponseFile.SourceDependencies != null)
            {
                descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.SourceDependencies.FullName);
            }
            if (msvcParsedResponseFile.ClangDepfile != null)
            {
                descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.ClangDepfile.FullName);
            }
            if (msvcParsedResponseFile.PchCacheFile != null && msvcParsedResponseFile.IsCreatingPch)
            {
                descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }

            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
