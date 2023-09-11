namespace Redpoint.Uet.Automation.TestReporter
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal sealed class DefaultTestReporterFactory : ITestReporterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTestReporterFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITestReporter CreateJunit(string path)
        {
            return new JunitTestReporter(
                _serviceProvider.GetRequiredService<ILogger<JunitTestReporter>>(),
                path);
        }

        public ITestReporter CreateNull()
        {
            return new NullTestReporter(
                _serviceProvider.GetRequiredService<ILogger<NullTestReporter>>());
        }
    }
}
