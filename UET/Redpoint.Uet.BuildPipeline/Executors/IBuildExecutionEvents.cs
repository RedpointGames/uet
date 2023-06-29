namespace Redpoint.Uet.BuildPipeline.Executors
{
    using System.Threading.Tasks;

    public interface IBuildExecutionEvents
    {
        Task OnNodeStarted(string nodeName);

        Task OnNodeOutputReceived(string nodeName, string[] lines);

        Task OnNodeFinished(string nodeName, BuildResultStatus resultStatus);
    }
}
