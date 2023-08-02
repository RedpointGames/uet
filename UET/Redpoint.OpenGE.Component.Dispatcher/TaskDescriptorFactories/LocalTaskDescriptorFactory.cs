namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Executor;
    using Redpoint.OpenGE.Protocol;

    internal class LocalTaskDescriptorFactory : ITaskDescriptorFactory
    {
        public int ScoreTaskSpec(GraphTaskSpec spec)
        {
            // We can run anything locally, but we'd prefer tasks to be remotable.
            return 0;
        }

        public ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(GraphTaskSpec spec)
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
