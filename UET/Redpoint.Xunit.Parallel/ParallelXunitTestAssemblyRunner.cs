namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelXunitTestAssemblyRunner : XunitTestAssemblyRunner
    {
        private readonly SemaphoreSlim _semaphore;

        public ParallelXunitTestAssemblyRunner(
            ITestAssembly testAssembly,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions) : base(
                testAssembly,
                testCases,
                diagnosticMessageSink,
                executionMessageSink,
                executionOptions)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(x => x.GetName().ToString() == TestAssembly.Assembly.Name);
            var desiredParallelism = assembly
                .GetCustomAttribute<UseParallelXunitTestFrameworkAttribute>()!
                .GetParallelismCount();
            _semaphore = new SemaphoreSlim(desiredParallelism);
        }

        protected override Task<RunSummary> RunTestCollectionAsync(
            IMessageBus messageBus,
            ITestCollection testCollection,
            IEnumerable<IXunitTestCase> testCases,
            CancellationTokenSource cancellationTokenSource)
        {
            return new ParallelXunitTestCollectionRunner(
                _semaphore,
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