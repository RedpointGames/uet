namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;

    internal class FileCopyTaskDescriptorFactory : ITaskDescriptorFactory
    {
        public int ScoreTaskSpec(GraphTaskSpec spec)
        {
            if (Path.GetFileName(spec.Tool.Path).Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                spec.Arguments.Length == 4 &&
                spec.Arguments[0] == "/c" &&
                spec.Arguments[1] == "copy")
            {
                // We really want to handle this.
                return 10000;
            }
            else
            {
                // We can't handle anything else.
                return -1;
            }
        }

        public ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken)
        {
            var from = spec.Arguments[2].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var to = spec.Arguments[3].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(from))
            {
                from = Path.Combine(spec.WorkingDirectory, from);
            }
            if (!Path.IsPathRooted(to))
            {
                to = Path.Combine(spec.WorkingDirectory, to);
            }
            return new ValueTask<TaskDescriptor>(new TaskDescriptor
            {
                Copy = new CopyTaskDescriptor
                {
                    FromAbsolutePath = from,
                    ToAbsolutePath = to,
                }
            });
        }
    }
}
