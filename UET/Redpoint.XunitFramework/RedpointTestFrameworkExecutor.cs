namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Reflection;

    internal class RedpointTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        public RedpointTestFrameworkExecutor(
            AssemblyName assemblyName,
            Xunit.Abstractions.ISourceInformationProvider sourceInformationProvider,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink) : base(
                assemblyName,
                sourceInformationProvider,
                diagnosticMessageSink)
        {
        }

        protected override async void RunTestCases(
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink executionMessageSink,
            Xunit.Abstractions.ITestFrameworkExecutionOptions executionOptions)
        {
            using var assemblyRunner = new RedpointTestAssemblyRunner(
                TestAssembly,
                testCases,
                DiagnosticMessageSink,
                executionMessageSink,
                executionOptions);
            await assemblyRunner.RunAsync();
        }
    }
}