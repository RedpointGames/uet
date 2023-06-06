namespace Redpoint.UET.Automation.Runner
{
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using System;
    using System.Collections.Generic;

    public interface IAutomationRunnerFactory
    {
        Task<IAutomationRunner> CreateAndRunAsync(
            ITestLogger logger,
            ITestNotification notification,
            ITestReporter reporter,
            IEnumerable<DesiredWorkerDescriptor> workerGroups,
            string testPrefix,
            TimeSpan? timeout,
            CancellationToken cancellationToken);
    }
}
