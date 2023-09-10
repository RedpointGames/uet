namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RedpointTestMethodRunner : XunitTestMethodRunner
    {
        private readonly Xunit.Abstractions.IMessageSink _diagnosticMessageSink;

        public RedpointTestMethodRunner(
            Xunit.Abstractions.ITestMethod testMethod,
            Xunit.Abstractions.IReflectionTypeInfo @class,
            Xunit.Abstractions.IReflectionMethodInfo method,
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource,
            object[] constructorArguments) : base(
                testMethod,
                @class,
                method,
                testCases,
                diagnosticMessageSink,
                messageBus,
                aggregator,
                cancellationTokenSource,
                constructorArguments)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
        {
            // Create a text representation of the test parameters (for theory tests)
            var parameters = string.Empty;

            if (testCase.TestMethodArguments != null)
            {
                parameters = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
            }

            // Build the full name of the test (class + method + parameters)
            var test = $"{TestMethod.TestClass.Class.Name}.{TestMethod.Method.Name}({parameters})";

            // Write a log to the output that we're starting the test
            _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"STARTED: {test}"));

            try
            {
                // Start a timer
                var deadlineMinutes = 2;
                using var timer = new Timer(
                    _ => _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"WARNING: {test} has been running for more than {deadlineMinutes} minutes")),
                    null,
                    TimeSpan.FromMinutes(deadlineMinutes),
                    Timeout.InfiniteTimeSpan);

                // Execute the test and get the result
                var result = await base.RunTestCaseAsync(testCase);

                // Work out the final status of the test
                var status = result.Failed > 0
                    ? "FAILURE"
                    : (result.Skipped > 0 ? "SKIPPED" : "SUCCESS");

                // Write the result of the test to the output
                _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"{status}: {test} ({result.Time}s)"));

                return result;
            }
            catch (Exception ex)
            {
                // Something went wrong trying to execute the test
                _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"ERROR: {test} ({ex.Message})"));
                throw;
            }
        }
    }
}