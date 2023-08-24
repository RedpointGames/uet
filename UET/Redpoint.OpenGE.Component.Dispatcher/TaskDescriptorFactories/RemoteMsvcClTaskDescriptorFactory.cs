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

        public string PreparationOperationDescription => "parsing headers";

        public string PreparationOperationCompletedDescription => "parsed headers";

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
                null,
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
