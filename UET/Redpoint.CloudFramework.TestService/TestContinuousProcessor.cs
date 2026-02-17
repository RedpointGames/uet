namespace Redpoint.CloudFramework.TestService
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class TestContinuousProcessor : IContinuousProcessor
    {
        private readonly ILogger<TestContinuousProcessor> _logger;

        public TestContinuousProcessor(ILogger<TestContinuousProcessor> logger)
        {
            _logger = logger;
        }

        public static string RoleName => "test";

        public async Task ExecuteAsync(CancellationToken shutdownCancellationToken)
        {
            _logger.LogInformation("This is an informational service message.");

            _logger.LogError("This is an error from a service.");

            throw new InvalidOperationException("This is an exception from a service.");
        }
    }
}
