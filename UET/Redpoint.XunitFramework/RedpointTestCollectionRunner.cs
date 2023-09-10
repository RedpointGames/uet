namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RedpointTestCollectionRunner : XunitTestCollectionRunner
    {
        public RedpointTestCollectionRunner(
            Xunit.Abstractions.ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ITestCaseOrderer testCaseOrderer,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource) : base(
                testCollection,
                testCases,
                diagnosticMessageSink,
                messageBus,
                testCaseOrderer,
                aggregator,
                cancellationTokenSource)
        {
        }

        protected override Task<RunSummary> RunTestClassAsync(
            Xunit.Abstractions.ITestClass testClass,
            Xunit.Abstractions.IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases)
        {
            return new RedpointTestClassRunner(
                testClass,
                @class,
                testCases,
                DiagnosticMessageSink,
                MessageBus,
                TestCaseOrderer,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource,
                CollectionFixtureMappings)
                .RunAsync();
        }
    }
}