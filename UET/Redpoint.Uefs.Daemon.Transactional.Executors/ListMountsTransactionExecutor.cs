namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Threading.Tasks;

    internal class ListMountsTransactionExecutor : ITransactionExecutor<ListMountsTransactionRequest, ListResponse>
    {
        private readonly IMountTracking _mountTracking;
        private readonly IMountLockObtainer _mountLockObtainer;

        public ListMountsTransactionExecutor(
            IMountTracking mountTracking,
            IMountLockObtainer mountLockObtainer)
        {
            _mountTracking = mountTracking;
            _mountLockObtainer = mountLockObtainer;
        }

        public async Task<ListResponse> ExecuteTransactionAsync(
            ITransactionContext<ListResponse> context,
            ListMountsTransactionRequest transaction,
            CancellationToken cancellationToken)
        {
            var response = new ListResponse();

            using (await _mountLockObtainer.ObtainLockAsync(
                context,
                "ListMountsTransactionExecutor",
                cancellationToken))
            {
                foreach (var kv in _mountTracking.CurrentMounts)
                {
                    response.Mounts.Add(kv.Value.GetMountDescriptor(kv.Key));
                }
            }

            return response;
        }
    }
}
