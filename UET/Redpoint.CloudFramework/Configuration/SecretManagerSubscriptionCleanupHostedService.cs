namespace Redpoint.CloudFramework.Configuration
{
    using Microsoft.Extensions.Hosting;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SecretManagerSubscriptionCleanupHostedService : IHostedService
    {
        private readonly ISecretManagerNotificationManager _secretManagerNotificationManager;

        public SecretManagerSubscriptionCleanupHostedService(
            ISecretManagerNotificationManager secretManagerNotificationManager)
        {
            _secretManagerNotificationManager = secretManagerNotificationManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _secretManagerNotificationManager.UnsubscribeAllAsync().ConfigureAwait(false);
        }
    }
}
