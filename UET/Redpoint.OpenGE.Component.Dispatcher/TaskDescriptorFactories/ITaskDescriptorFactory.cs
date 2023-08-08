namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;
    using System.Threading;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;

    internal interface ITaskDescriptorFactory
    {
        string? PreparationOperationDescription => null;

        string? PreparationOperationCompletedDescription => null;

        int ScoreTaskSpec(GraphTaskSpec spec);

        ValueTask<TaskDescriptor> CreateDescriptorForTaskSpecAsync(
            GraphTaskSpec spec, 
            CancellationToken cancellationToken);
    }
}
