namespace Redpoint.Uet.Automation.Runner
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DefaultAutomationRunnerFactory : IAutomationRunnerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultAutomationRunnerFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task<IAutomationRunner> CreateAndRunAsync(
            ITestLogger logger,
            ITestNotification notification,
            ITestReporter reporter,
            IEnumerable<DesiredWorkerDescriptor> workerGroups,
            AutomationRunnerConfiguration configuration,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IAutomationRunner>(new DefaultAutomationRunner(
                _serviceProvider.GetRequiredService<ILogger<DefaultAutomationRunner>>(),
                _serviceProvider.GetRequiredService<IWorkerPoolFactory>(),
                logger,
                notification,
                reporter,
                workerGroups,
                configuration,
                cancellationToken));
        }
    }
}
