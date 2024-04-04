namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Protocol;
    using System.Diagnostics;
    using System.Threading;

    internal class FileCopyTaskDescriptorFactory : ITaskDescriptorFactory
    {
        public int ScoreTaskSpec(GraphTaskSpec spec)
        {
            if (Path.GetFileName(spec.Tool.Path).Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                spec.Arguments.Length == 2 &&
                spec.Arguments[0].Equals("/C", StringComparison.OrdinalIgnoreCase))
            {
                var realArguments = CommandLineArgumentSplitter.SplitArguments(spec.Arguments[1]);
                if (realArguments.Length >= 4 &&
                    realArguments[0].Equals("copy", StringComparison.OrdinalIgnoreCase) &&
                    realArguments[1].Equals("/Y", StringComparison.OrdinalIgnoreCase))
                {
                    // We really want to handle this.
                    return 10000;
                }
            }

            // We can't handle anything else.
            return -1;
        }

        public ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec,
            bool guaranteedToExecuteLocally,
            CancellationToken cancellationToken)
        {
            var realArguments = CommandLineArgumentSplitter.SplitArguments(spec.Arguments[1]);

            var from = realArguments[2].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var to = realArguments[3].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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
