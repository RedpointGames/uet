namespace Redpoint.XunitFramework
{
    using Xunit.Sdk;
    using System.Collections.Generic;
    using System.Reflection;
    using Xunit.Abstractions;
    using System.Diagnostics;

    internal class RedpointTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public RedpointTestFrameworkExecutor(
            AssemblyName assemblyName,
            Xunit.Abstractions.ISourceInformationProvider sourceInformationProvider,
            Xunit.Abstractions.IMessageSink diagnosticMessageSink) : base(
                assemblyName,
                sourceInformationProvider,
                diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        protected override async void RunTestCases(
            IEnumerable<IXunitTestCase> testCases,
            Xunit.Abstractions.IMessageSink executionMessageSink,
            Xunit.Abstractions.ITestFrameworkExecutionOptions executionOptions)
        {
            var testCasesList = testCases.ToList();
            using var assemblyRunner = new RedpointTestAssemblyRunner(
                TestAssembly,
                testCasesList,
                DiagnosticMessageSink,
                executionMessageSink,
                executionOptions);
            _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"RUN STARTED: Redpoint.XunitFramework has been asked to run {testCasesList.Count} test cases in process ID {Environment.ProcessId}..."));
            await assemblyRunner.RunAsync();
            _diagnosticMessageSink.OnMessage(new DiagnosticMessage($"RUN STARTED: Redpoint.XunitFramework has finished running all test cases."));
        }
    }
}