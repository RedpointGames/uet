namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RemoteGenericTaskDescriptorFactory : RemoteTaskDescriptorFactory
    {
        private ISet<string> _recognisedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cl.exe",
        };

        public override string? PreparationOperationDescription => null;

        public override string? PreparationOperationCompletedDescription => null;

        public override int ScoreTaskSpec(GraphTaskSpec spec)
        {
            // Handle these tools.
            var filename = Path.GetFileName(spec.Tool.Path);
            if (_recognisedTools.Contains(filename))
            {
                return 2000;
            }

            // Don't handle anything else at the moment.
            return -1;
        }

        public override ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(GraphTaskSpec spec, bool guaranteedToExecuteLocally, CancellationToken cancellationToken)
        {
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

            // Return the remote task descriptor.
            var descriptor = new RemoteTaskDescriptor();
            descriptor.ToolLocalAbsolutePath = spec.Tool.Path;
            descriptor.Arguments.AddRange(spec.Arguments);
            descriptor.EnvironmentVariables.MergeFrom(environmentVariables);
            descriptor.WorkingDirectoryAbsolutePath = spec.WorkingDirectory;
            descriptor.UseFastLocalExecution = guaranteedToExecuteLocally;
            descriptor.RemoteFsStorageLayer = new RemoteFsStorageLayer();

            return ValueTask.FromResult(new TaskDescriptor { Remote = descriptor });
        }
    }
}
