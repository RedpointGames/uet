namespace Redpoint.XunitFramework
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit.Sdk;
    using Xunit.v3;

    internal class RedpointTestFrameworkExecutor : XunitTestFrameworkExecutor
    {
        public RedpointTestFrameworkExecutor(IXunitTestAssembly testAssembly) : base(testAssembly)
        {
        }

        public override async ValueTask RunTestCases(
            IReadOnlyCollection<IXunitTestCase> testCases, 
            IMessageSink executionMessageSink, 
            ITestFrameworkExecutionOptions executionOptions, 
            CancellationToken cancellationToken)
        {
            var testCasesList = testCases.ToList();

            executionMessageSink.OnMessage(new DiagnosticMessage($"RUN STARTED: Redpoint.XunitFramework has been asked to run {testCasesList.Count} test cases in process ID {Environment.ProcessId}..."));
            await RedpointTestAssemblyRunner.RedpointInstance.Run(
                TestAssembly,
                testCasesList, 
                executionMessageSink,
                executionOptions, 
                cancellationToken);
            executionMessageSink.OnMessage(new DiagnosticMessage($"RUN STARTED: Redpoint.XunitFramework has finished running all test cases."));
        }
    }
}