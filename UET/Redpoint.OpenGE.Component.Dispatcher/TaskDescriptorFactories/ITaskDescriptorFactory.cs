namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;
    using System.Threading;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;

    internal interface ITaskDescriptorFactory
    {
        int ScoreTaskSpec(GraphTaskSpec spec);

        ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec, 
            CancellationToken cancellationToken);
    }
}
