namespace Redpoint.XunitFramework
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Internal;
    using Xunit.Sdk;
    using Xunit.v3;

    internal class RedpointTestMethodRunner : XunitTestMethodRunner
    {
        public static RedpointTestMethodRunner RedpointInstance { get; } = new();

        protected override async ValueTask<RunSummary> RunTestCase(
            XunitTestMethodRunnerContext ctxt, 
            IXunitTestCase testCase)
        {
            Guard.ArgumentNotNull(ctxt);
            Guard.ArgumentNotNull(testCase);

            // Create a text representation of the test parameters (for theory tests)
            var parameters = string.Empty;

            // @todo
            /*
            if (testCase.TestMethodArguments != null)
            {
                parameters = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
            }
            */

            // Build the full name of the test (class + method + parameters)
            var test = $"{testCase.TestClassName}.{testCase.TestMethodName}({parameters})";

            // Write a log to the output that we're starting the test
            ctxt.MessageBus.QueueMessage(new DiagnosticMessage($"STARTED: {test}"));

            try
            {
                // Start a timer
                var deadlineMinutes = 2;
                using var timer = new Timer(
                    _ => ctxt.MessageBus.QueueMessage(new DiagnosticMessage($"WARNING: {test} has been running for more than {deadlineMinutes} minutes")),
                    null,
                    TimeSpan.FromMinutes(deadlineMinutes),
                    Timeout.InfiniteTimeSpan);

                // Execute the test and get the result
                ValueTask<RunSummary> runTask;
                if (testCase is ISelfExecutingXunitTestCase selfExecutingTestCase)
                {
                    runTask = selfExecutingTestCase.Run(ctxt.ExplicitOption, ctxt.MessageBus, ctxt.ConstructorArguments, ctxt.Aggregator.Clone(), ctxt.CancellationTokenSource);
                }
                else
                {
                    runTask = XunitRunnerHelper.RunXunitTestCase(
                        testCase,
                        ctxt.MessageBus,
                        ctxt.CancellationTokenSource,
                        ctxt.Aggregator.Clone(),
                        ctxt.ExplicitOption,
                        ctxt.ConstructorArguments
                    );
                }
                var result = await runTask.ConfigureAwait(false);

                // Work out the final status of the test
                var status = result.Failed > 0
                    ? "FAILURE"
                    : (result.Skipped > 0 ? "SKIPPED" : "SUCCESS");

                // Write the result of the test to the output
                ctxt.MessageBus.QueueMessage(new DiagnosticMessage($"{status}: {test} ({result.Time}s)"));

                return result;
            }
            catch (Exception ex)
            {
                // Something went wrong trying to execute the test
                ctxt.MessageBus.QueueMessage(new DiagnosticMessage($"ERROR: {test} ({ex.Message})"));
                throw;
            }
        }
    }
}