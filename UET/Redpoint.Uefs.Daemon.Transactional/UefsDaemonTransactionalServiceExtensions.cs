namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;

    public static class UefsDaemonTransactionalServiceExtensions
    {
        public static void AddUefsDaemonTransactional(this IServiceCollection services)
        {
            services.AddSingleton<ITransactionalDatabaseFactory, DefaultTransactionalDatabaseFactory>();

            services.AddSingleton<IMountLockObtainer, DefaultMountLockObtainer>();
        }
    }
}
