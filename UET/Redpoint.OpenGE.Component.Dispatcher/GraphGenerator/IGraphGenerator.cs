namespace Redpoint.OpenGE.Component.Dispatcher.GraphGenerator
{
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.JobXml;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IGraphGenerator
    {
        Task<Graph> GenerateGraphFromJob(
            Job job,
            GraphExecutionEnvironment graphExecutionEnvironment,
            CancellationToken cancellationToken);
    }
}
