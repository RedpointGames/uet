namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Core;
    using System.IO;
    using System.Threading;
    using Tenray.ZoneTree.Logger;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc;

    internal class RemoteMsvcClTaskDescriptorFactory : RemoteTaskDescriptorFactory
    {
        private readonly ILogger<RemoteMsvcClTaskDescriptorFactory> _logger;
        private readonly LocalTaskDescriptorFactory _localTaskDescriptorFactory;
        private readonly IMsvcResponseFileParser _msvcResponseFileParser;

        public RemoteMsvcClTaskDescriptorFactory(
            ILogger<RemoteMsvcClTaskDescriptorFactory> logger,
            LocalTaskDescriptorFactory localTaskDescriptorFactory,
            IMsvcResponseFileParser msvcResponseFileParser)
        {
            _logger = logger;
            _localTaskDescriptorFactory = localTaskDescriptorFactory;
            _msvcResponseFileParser = msvcResponseFileParser;
        }

        public override string PreparationOperationDescription => "parsing headers";

        public override string PreparationOperationCompletedDescription => "parsed headers";

        public override int ScoreTaskSpec(GraphTaskSpec spec)
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

        public override async ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken)
        {
            // Parse the response file.
            var msvcParsedResponseFile = await _msvcResponseFileParser.ParseResponseFileAsync(
                spec.Arguments[0].Substring(1),
                spec.WorkingDirectory,
                guaranteedToExecuteLocally,
                spec.ExecutionEnvironment.BuildStartTicks,
                new CompilerArchitype
                {
                    Msvc = new MsvcCompiler
                    {
                        CppLanguageVersion = 201703 /* C++ 17 */,
                        CLanguageVersion = 201710 /* C 17 */,
                        MsvcVersion = 1935 /* 2022 17.5 */,
                        MsvcFullVersion = 193599999 /* Latest 2022 17.5 */,
                    },
                    TargetPlatformNumericDefines =
                    {
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
                // Delegate to the local executor.
                return await _localTaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(
                    spec,
                    guaranteedToExecuteLocally,
                    cancellationToken);
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
            descriptor.Arguments.Add("@" + msvcParsedResponseFile.ResponseFilePath);
            descriptor.EnvironmentVariables.MergeFrom(environmentVariables);
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            descriptor.UseFastLocalExecution = guaranteedToExecuteLocally;
            var inputsByPathOrContent = new InputFilesByPathOrContent();
            if (msvcParsedResponseFile.PchCacheFile != null && !msvcParsedResponseFile.IsCreatingPch)
            {
                inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }
            inputsByPathOrContent.AbsolutePaths.AddRange(msvcParsedResponseFile.DependentFiles.DependsOnPaths);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.ResponseFilePath);
            inputsByPathOrContent.AbsolutePaths.Add(msvcParsedResponseFile.InputFile.FullName);
            descriptor.InputsByPathOrContent = inputsByPathOrContent;
            descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.OutputFile.FullName);
            if (msvcParsedResponseFile.SourceDependencies != null)
            {
                descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.SourceDependencies.FullName);
            }
            if (msvcParsedResponseFile.PchCacheFile != null && msvcParsedResponseFile.IsCreatingPch)
            {
                descriptor.OutputAbsolutePaths.Add(msvcParsedResponseFile.PchCacheFile.FullName);
            }

            return new TaskDescriptor { Remote = descriptor };
        }
    }
}
