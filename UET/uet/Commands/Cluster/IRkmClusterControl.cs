namespace UET.Commands.Cluster
{
    using Redpoint.CommandLine;
    using System.CommandLine.Invocation;

    internal interface IRkmClusterControl
    {
        Task<int> CreateOrJoin(ICommandInvocationContext context, ClusterOptions options);

        Task StreamLogs(CancellationToken cancellationToken);
    }
}
