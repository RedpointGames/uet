namespace UET.Commands.TestPackaged
{
    using System.Threading.Tasks;
    using System.Net;
    using Grpc.Core.Logging;

    internal interface IAutomationRunner
    {
        Task<int> RunTestsAsync(
            IPEndPoint endpoint,
            string testPrefix,
            string projectName,
            CancellationToken cancellationToken);
    }
}
