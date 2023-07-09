namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelXunitTestMethodRunner : XunitTestMethodRunner
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly IMessageSink _diagnosticMessageSink;
        private readonly object[] _constructorArguments;

        public ParallelXunitTestMethodRunner(
            SemaphoreSlim semaphore,
            ITestMethod testMethod,
            IReflectionTypeInfo @class,
            IReflectionMethodInfo method,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
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
            _semaphore = semaphore;
            _diagnosticMessageSink = diagnosticMessageSink;
            _constructorArguments = constructorArguments;
        }

        protected override async Task<RunSummary> RunTestCasesAsync()
        {
            RunSummary summary = new RunSummary();

            await Parallel.ForEachAsync(
                TestCases,
                CancellationTokenSource.Token,
                async (testCase, ct) =>
                {
                    await _semaphore.WaitAsync(ct);
                    try
                    {
                        summary.Aggregate(await testCase.RunAsync(
                            _diagnosticMessageSink,
                            MessageBus,
                            _constructorArguments.Select(x =>
                            {
                                if (x is ITestOutputHelper)
                                {
                                    return new TestOutputHelper();
                                }
                                else
                                {
                                    return x;
                                }
                            }).ToArray(),
                            new ExceptionAggregator(Aggregator),
                            CancellationTokenSource));
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });

            return summary;
        }

        protected override Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
        {
            throw new NotSupportedException();
        }
    }
}