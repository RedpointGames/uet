namespace Redpoint.Xunit.Parallel
{
    using global::Xunit.Abstractions;
    using global::Xunit.Sdk;
    using System.Collections.Generic;
    using System.Reflection;

    public class ParallelXunitTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        public ParallelXunitTestFrameworkExecutor(
            AssemblyName assemblyName,
            ISourceInformationProvider sourceInformationProvider,
            IMessageSink diagnosticMessageSink) : base(
                assemblyName,
                sourceInformationProvider,
                diagnosticMessageSink)
        {
        }

        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using (var assemblyRunner = new ParallelXunitTestAssemblyRunner(
                TestAssembly,
                testCases,
                DiagnosticMessageSink,
                executionMessageSink,
                executionOptions))
            {
                await assemblyRunner.RunAsync();
            }
        }
    }
}