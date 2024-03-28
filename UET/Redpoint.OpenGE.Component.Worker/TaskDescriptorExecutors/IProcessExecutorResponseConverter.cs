namespace Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors
{
    using Redpoint.OpenGE.Protocol;
    using ProcessResponse = ProcessExecution.Enumerable.ProcessResponse;

    internal interface IProcessExecutorResponseConverter
    {
        ExecuteTaskResponse ConvertResponse(ProcessResponse response);
    }
}
