namespace Redpoint.XunitFramework
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Internal;
    using Xunit.Sdk;
    using Xunit.v3;

    internal class RedpointTestAssemblyRunner : XunitTestAssemblyRunner
    {
        public static RedpointTestAssemblyRunner RedpointInstance { get; } = new();

        protected override ValueTask<RunSummary> RunTestCollection(
            XunitTestAssemblyRunnerContext ctxt, 
            IXunitTestCollection testCollection, 
            IReadOnlyCollection<IXunitTestCase> testCases)
        {
            Guard.ArgumentNotNull(ctxt);
            Guard.ArgumentNotNull(testCollection);
            Guard.ArgumentNotNull(testCases);

            var testCaseOrderer = ctxt.AssemblyTestCaseOrderer ?? DefaultTestCaseOrderer.Instance;

            return RedpointTestCollectionRunner.RedpointInstance.Run(
                testCollection,
                testCases,
                ctxt.ExplicitOption,
                ctxt.MessageBus,
                testCaseOrderer,
                ctxt.Aggregator.Clone(),
                ctxt.CancellationTokenSource,
                ctxt.AssemblyFixtureMappings
            );
        }
    }
}