namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;

    public static class UefsDaemonTransactionalExecutorsServiceExtensions
    {
        public static void AddUefsDaemonTransactionalExecutors(this IServiceCollection services)
        {
            services.AddTransient<ITransactionExecutor<AddMountTransactionRequest>, AddMountTransactionExecutor>();
            services.AddTransient<ITransactionExecutor<RemoveMountTransactionRequest>, RemoveMountTransactionExecutor>();
            services.AddTransient<ITransactionExecutor<ListMountsTransactionRequest, ListResponse>, ListMountsTransactionExecutor>();
            services.AddTransient<ITransactionExecutor<PullGitCommitTransactionRequest>, PullGitCommitTransactionExecutor>();
            services.AddTransient<ITransactionExecutor<PullPackageTagTransactionRequest, PullPackageTagTransactionResult>, PullPackageTagTransactionExecutor>();
            services.AddTransient<ITransactionExecutor<VerifyPackagesTransactionRequest>, VerifyPackagesTransactionExecutor>();
        }
    }
}
