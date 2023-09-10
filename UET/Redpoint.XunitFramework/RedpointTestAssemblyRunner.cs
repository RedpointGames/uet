namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RedpointTestAssemblyRunner : XunitTestAssemblyRunner
    {
        public RedpointTestAssemblyRunner(
            Xunit.Abstractions.ITestAssembly testAssembly,
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink,
            Xunit.Abstractions.IMessageSink executionMessageSink,
            Xunit.Abstractions.ITestFrameworkExecutionOptions executionOptions) : base(
                testAssembly,
                testCases,
                diagnosticMessageSink,
                executionMessageSink,
                executionOptions)
        {
        }

        protected override Task<RunSummary> RunTestCollectionAsync(
            IMessageBus messageBus,
            Xunit.Abstractions.ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            CancellationTokenSource cancellationTokenSource)
        {
            return new RedpointTestCollectionRunner(
                testCollection,
                testCases,
                DiagnosticMessageSink,
                messageBus,
                TestCaseOrderer,
                new ExceptionAggregator(Aggregator),
                cancellationTokenSource).RunAsync();
        }
    }
}