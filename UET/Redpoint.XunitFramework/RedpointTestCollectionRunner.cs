namespace Redpoint.XunitFramework
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Internal;
    using Xunit.Sdk;
    using Xunit.v3;

    internal class RedpointTestCollectionRunner : XunitTestCollectionRunner
    {
        public static RedpointTestCollectionRunner RedpointInstance { get; } = new();

        protected override ValueTask<RunSummary> RunTestClass(
            XunitTestCollectionRunnerContext ctxt, 
            IXunitTestClass? testClass, 
            IReadOnlyCollection<IXunitTestCase> testCases)
        {
            Guard.ArgumentNotNull(ctxt);
            Guard.ArgumentNotNull(testCases);

            if (testClass is null)
                return new(XunitRunnerHelper.FailTestCases(
                    ctxt.MessageBus,
                    ctxt.CancellationTokenSource,
                    testCases,
                    "Test case '{0}' does not have an associated class and cannot be run by XunitTestClassRunner",
                    sendTestClassMessages: true,
                    sendTestMethodMessages: true
                ));

            return RedpointTestClassRunner.RedpointInstance.Run(
                testClass,
                testCases,
                ctxt.ExplicitOption,
                ctxt.MessageBus,
                ctxt.TestCaseOrderer,
                ctxt.Aggregator.Clone(),
                ctxt.CancellationTokenSource,
                ctxt.CollectionFixtureMappings
            );
        }
    }
}