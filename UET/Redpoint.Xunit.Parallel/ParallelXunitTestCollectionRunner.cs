namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelXunitTestCollectionRunner : XunitTestCollectionRunner
    {
        public ParallelXunitTestCollectionRunner(
            ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
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
            ITestClass testClass,
            IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases)
        {
            return new ParallelXunitTestClassRunner(
                testClass,
                @class,
                testCases,
                DiagnosticMessageSink,
                MessageBus,
                TestCaseOrderer,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource,
                CollectionFixtureMappings).RunAsync();
        }
    }
}