namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RedpointTestClassRunner : XunitTestClassRunner
    {
        public RedpointTestClassRunner(
            Xunit.Abstractions.ITestClass testClass,
            Xunit.Abstractions.IReflectionTypeInfo @class,
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink,
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

        protected override Task<RunSummary> RunTestMethodAsync(
            Xunit.Abstractions.ITestMethod testMethod,
            Xunit.Abstractions.IReflectionMethodInfo method,
            IEnumerable<IXunitTestCase> testCases,
            object[] constructorArguments)
        {
            return new RedpointTestMethodRunner(
                testMethod,
                Class,
                method,
                testCases,
                DiagnosticMessageSink,
                MessageBus,
                new ExceptionAggregator(Aggregator),
                CancellationTokenSource,
                constructorArguments)
                .RunAsync();
        }
    }
}