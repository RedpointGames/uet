namespace UET.Commands.Cluster
{
    internal interface IRkmClusterControl
    {
        Task<int> CreateOrJoin(bool create);

        Task StreamLogs(CancellationToken cancellationToken);
    }
}
