namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelXunitTestCollectionRunner : XunitTestCollectionRunner
    {
        private readonly SemaphoreSlim _semaphore;

        public ParallelXunitTestCollectionRunner(
            SemaphoreSlim semaphore,
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
            _semaphore = semaphore;
        }

        protected override async Task<RunSummary> RunTestClassesAsync()
        {
            var summary = new RunSummary();

            await Parallel.ForEachAsync(
                TestCases.GroupBy(tc => tc.TestMethod.TestClass, TestClassComparer.Instance),
                CancellationTokenSource.Token,
                async (testCasesByClass, ct) =>
                {
                    var result = await RunTestClassAsync(testCasesByClass.Key, (IReflectionTypeInfo)testCasesByClass.Key.Class, testCasesByClass);
                    summary.Aggregate(result);
                });

            return summary;
        }

        protected override Task<RunSummary> RunTestClassAsync(
            ITestClass testClass,
            IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases)
        {
            return new ParallelXunitTestClassRunner(
                _semaphore,
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