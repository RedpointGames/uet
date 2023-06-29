namespace Redpoint.Uet.Automation.TestNotification
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.TestNotification.Io;

    internal class DefaultTestNotificationFactory : ITestNotificationFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTestNotificationFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITestNotification CreateIo(CancellationToken cancellationToken)
        {
            return new IoTestNotification(
                _serviceProvider.GetRequiredService<ILogger<IoTestNotification>>(),
                cancellationToken,
                !IoTestNotification.IsIoAvailable());
        }

        public ITestNotification CreateNull()
        {
            return new NullTestNotification();
        }
    }
}
