namespace Redpoint.CloudFramework.TestService
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Processor;
    using Redpoint.CloudFramework.Tracing;
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class TestContinuousProcessor : IContinuousProcessor
    {
        private readonly ILogger<TestContinuousProcessor> _logger;
        private readonly IManagedTracer _managedTracer;

        public TestContinuousProcessor(
            ILogger<TestContinuousProcessor> logger,
            IManagedTracer managedTracer)
        {
            _logger = logger;
            _managedTracer = managedTracer;
        }

        public static string RoleName => "test";

        public async Task ExecuteAsync(CancellationToken shutdownCancellationToken)
        {
            using (var span = _managedTracer.StartSpan("test-span"))
            {
                _logger.LogInformation("This is an informational service message.");

                _logger.LogError("This is an error from a service.");
            }

            throw new InvalidOperationException("This is an exception from a service.");
        }
    }
}
