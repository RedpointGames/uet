namespace Redpoint.Uet.Automation.TestLogging
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.TestLogger;

    internal sealed class DefaultTestLoggerFactory : ITestLoggerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTestLoggerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITestLogger CreateConsole()
        {
            // Handle log forwarding.
            var pipeName = Environment.GetEnvironmentVariable("UET_AUTOMATION_LOGGER_PIPE_NAME");
            if (!string.IsNullOrWhiteSpace(pipeName))
            {
                return new GrpcTestLogger(pipeName);
            }

            return new ConsoleTestLogger(
                _serviceProvider.GetRequiredService<ILogger<ConsoleTestLogger>>());
        }

        public ITestLogger CreateNull()
        {
            return new NullTestLogger();
        }
    }
}