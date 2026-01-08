namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using System;
    using System.Threading.Tasks;

    internal interface IDurableOperation
    {
        Task DurableOperationAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken);

        Task<TOut> DurableOperationAsync<TOut>(
            Func<CancellationToken, Task<TOut>> operation,
            CancellationToken cancellationToken);
    }
}
