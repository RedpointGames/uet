namespace Redpoint.UET.Automation.Runner
{
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using System;
    using System.Collections.Generic;

    public record class AutomationRunnerConfiguration
    {
        /// <summary>
        /// The name of the project being tested.
        /// </summary>
        public required string ProjectName { get; set; }

        /// <summary>
        /// The prefix to use for picking what tests to run.
        /// </summary>
        public required string TestPrefix { get; set; }

        /// <summary>
        /// The leading filename prefix to cut when reporting on filenames in test results.
        /// </summary>
        public required string FilenamePrefixToCut { get; set; }

        /// <summary>
        /// The timeout for the whole test run.
        /// </summary>
        public TimeSpan? TestRunTimeout { get; set; }

        /// <summary>
        /// The timeout for an individual test.
        /// </summary>
        public TimeSpan? TestTimeout { get; set; }

        /// <summary>
        /// The maximum number of times a test will be attempted to get it to pass.
        /// </summary>
        public int? TestAttemptCount { get; set; }
    }

    public interface IAutomationRunnerFactory
    {
        Task<IAutomationRunner> CreateAndRunAsync(
            ITestLogger logger,
            ITestNotification notification,
            ITestReporter reporter,
            IEnumerable<DesiredWorkerDescriptor> workerGroups,
            AutomationRunnerConfiguration configuration,
            CancellationToken cancellationToken);
    }
}
