namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelXunitTestClassRunner : XunitTestClassRunner
    {
        public ParallelXunitTestClassRunner(
            ITestClass testClass,
            IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            ITestCaseOrderer testCaseOrderer,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource,
            IDictionary<Type, object> collectionFixtureMappings) : base(
                testClass,
                @class,
                testCases,
                diagnosticMessageSink,
                messageBus,
                testCaseOrderer,
                aggregator,
                cancellationTokenSource,
                collectionFixtureMappings)
        {
        }

        static Exception Unwrap(Exception ex)
        {
            while (true)
            {
                var tiex = ex as TargetInvocationException;
                if (tiex == null)
                    return ex!;

                ex = tiex.InnerException!;
            }
        }

        protected override async Task<RunSummary> RunTestMethodsAsync()
        {
            var summary = new RunSummary();
            IEnumerable<IXunitTestCase> orderedTestCases;
            try
            {
                orderedTestCases = TestCaseOrderer.OrderTestCases(TestCases);
            }
            catch (Exception ex)
            {
                var innerEx = Unwrap(ex);
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Test case orderer '{TestCaseOrderer.GetType().FullName}' threw '{innerEx.GetType().FullName}' during ordering: {innerEx.Message}{Environment.NewLine}{innerEx.StackTrace}"));
                orderedTestCases = TestCases.ToList();
            }

            var constructorArguments = CreateTestClassConstructorArguments();

            await Parallel.ForEachAsync(
                orderedTestCases.GroupBy(tc => tc.TestMethod, TestMethodComparer.Instance),
                CancellationTokenSource.Token,
                async (method, ct) =>
                {
                    var result = await RunTestMethodAsync(
                        method.Key,
                        (IReflectionMethodInfo)method.Key.Method,
                        method,
                        constructorArguments);
                    summary.Aggregate(result);
                });

            return summary;
        }
    }
}