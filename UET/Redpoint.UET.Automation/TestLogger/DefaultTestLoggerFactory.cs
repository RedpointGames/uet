namespace Redpoint.UET.Automation.TestLogging
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class DefaultTestLoggerFactory : ITestLoggerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTestLoggerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITestLogger CreateConsole()
        {
            return new ConsoleTestLogger(
                _serviceProvider.GetRequiredService<ILogger<ConsoleTestLogger>>());
        }

        public ITestLogger CreateNull()
        {
            return new NullTestLogger();
        }
    }
}