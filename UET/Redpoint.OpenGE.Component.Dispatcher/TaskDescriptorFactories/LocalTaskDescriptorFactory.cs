namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;

    internal class LocalTaskDescriptorFactory : ITaskDescriptorFactory
    {
        public int ScoreTaskSpec(GraphTaskSpec spec)
        {
            // We can run anything locally, but we'd prefer tasks to be remotable.
            return 0;
        }

        public ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(GraphTaskSpec spec, CancellationToken cancellationToken)
        {
            var local = new LocalTaskDescriptor
            {
                Path = spec.Tool.Path,
            };
            local.Arguments.AddRange(spec.Arguments);
            local.EnvironmentVariables.MergeFrom(spec.ExecutionEnvironment.EnvironmentVariables);
            local.EnvironmentVariables.MergeFrom(spec.Environment.Variables);
            local.WorkingDirectory = spec.WorkingDirectory;
            return new ValueTask<TaskDescriptor>(new TaskDescriptor
            {
                Local = local
            });
        }
    }
}
