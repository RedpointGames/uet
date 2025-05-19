namespace UET.Commands.Cluster
{
    using System.CommandLine.Invocation;

    internal interface IRkmClusterControl
    {
        Task<int> CreateOrJoin(InvocationContext context, ClusterOptions options);

        Task StreamLogs(CancellationToken cancellationToken);
    }
}
