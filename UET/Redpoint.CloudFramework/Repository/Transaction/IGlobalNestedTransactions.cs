namespace Redpoint.CloudFramework.Repository.Transaction
{
    using Redpoint.CloudFramework.Repository.Metrics;
    using System.Threading.Tasks;

    public static class NestedTransactionExtensions
    {
        public static async Task<IModelTransaction> BeginPotentiallyNestedTransactionAsync(
            this IGlobalRepository globalRepository,
            string @namespace,
            IModelTransaction? existingTransaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(globalRepository);

            if (existingTransaction == null)
            {
                return await globalRepository.BeginTransactionAsync(@namespace, metrics: metrics, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (existingTransaction.Namespace == @namespace)
            {
                return new NestedModelTransaction(existingTransaction);
            }
            else
            {
                throw new InvalidOperationException("Cross-namespace nested transaction attempted!");
            }
        }
    }
}
