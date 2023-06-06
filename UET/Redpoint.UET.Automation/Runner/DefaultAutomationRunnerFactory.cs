namespace Redpoint.UET.Automation.Runner
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultAutomationRunnerFactory : IAutomationRunnerFactory
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
            string testPrefix,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IAutomationRunner>(new DefaultAutomationRunner(
                _serviceProvider.GetRequiredService<ILogger<DefaultAutomationRunner>>(),
                _serviceProvider.GetRequiredService<IWorkerPoolFactory>(),
                logger,
                notification,
                reporter,
                workerGroups,
                testPrefix,
                timeout,
                cancellationToken));
        }
    }
}
