namespace Redpoint.XunitFramework
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Internal;
    using Xunit.Sdk;
    using Xunit.v3;

    internal class RedpointTestClassRunner : XunitTestClassRunner
    {
        public static RedpointTestClassRunner RedpointInstance { get; } = new();

        protected override ValueTask<RunSummary> RunTestMethod(
            XunitTestClassRunnerContext ctxt,
            IXunitTestMethod? testMethod, 
            IReadOnlyCollection<IXunitTestCase> testCases,
            object?[] constructorArguments)
        {
            Guard.ArgumentNotNull(ctxt);

            // Technically not possible because of the design of TTestClass, but this signature is imposed
            // by the base class, which allows method-less tests
            if (testMethod is null)
                return new(XunitRunnerHelper.FailTestCases(
                    ctxt.MessageBus,
                    ctxt.CancellationTokenSource,
                    testCases,
                    "Test case '{0}' does not have an associated method and cannot be run by XunitTestMethodRunner",
                    sendTestMethodMessages: true
                ));

            return RedpointTestMethodRunner.RedpointInstance.Run(
                testMethod,
                testCases,
                ctxt.ExplicitOption,
                ctxt.MessageBus,
                ctxt.Aggregator.Clone(),
                ctxt.CancellationTokenSource,
                constructorArguments
            );
        }
    }
}