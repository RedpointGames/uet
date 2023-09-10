namespace Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics
{
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using System.Threading.Tasks;

    internal interface IStallDiagnostics
    {
        Task<string> CaptureStallInformationAsync(
            GraphExecutionInstance instance);
    }
}
