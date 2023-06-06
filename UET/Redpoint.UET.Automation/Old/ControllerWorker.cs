#if FALSE

namespace Redpoint.UET.Automation.Old
{
    using Redpoint.UET.Automation.TestLogging;

    public class ControllerWorker : Worker
    {
        public ControllerWorker(
            ITestLogger testLogger,
            string sessionGuid,
            string sessionName,
            int workerNum,
            string enginePath,
            string projectRoot,
            string projectPath,
            ICrashHooks crashHooks) : base(
                testLogger,
                sessionGuid,
                sessionName,
                workerNum,
                enginePath,
                projectRoot,
                projectPath,
                crashHooks)
        {
        }

        public string TestPrefix { get; set; } = string.Empty;

        public string TestReportJsonPath => Path.Combine(ProjectRoot, "Intermediate", "TestReport", "index.json");

        protected override IEnumerable<string> GetAdditionalArguments(bool isUnrealEngine5)
        {
            var arguments = new List<string>
            {
                $"-ReportExportPath={Path.Combine(ProjectRoot, "Intermediate", "TestReport")}",
                $"-ExecCmds=Automation RunTests {TestPrefix};Quit"
            };

            if (isUnrealEngine5)
            {
                // Unreal Engine 5 requires -ResumeRunTest to get it to emit the index.json file
                // repeatedly as the tests status change.
                arguments.Add("-ResumeRunTest");
            }

            return arguments;
        }
    }
}

#endif