namespace Redpoint.Uet.Uat.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Core;

    public class UATExecutionTests
    {
        [Fact]
        public async Task TestUATExecutionOfBuildGraphHelpWorks()
        {
            var enginePath = Environment.GetEnvironmentVariable("UET_ENGINE_PATH") ?? @"E:\EpicGames\UE_5.2";
            Assert.SkipUnless(Directory.Exists(enginePath), $"Engine must exist at {enginePath} for this test to run.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUetCore();
            services.AddUETUAT();

            var serviceProvider = services.BuildServiceProvider();
            var executor = serviceProvider.GetRequiredService<IUATExecutor>();

            var lines = new List<string>();
            var exitCode = await executor.ExecuteAsync(
                enginePath,
                new UATSpecification
                {
                    Command = "BuildGraph",
                    Arguments = new LogicalProcessArgument[] { "-help" },
                },
                CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates()
                {
                    ReceiveStdout = (line) =>
                    {
                        lines.Add(line);
                        return false;
                    }
                }),
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains(lines, x => x.Contains("-WriteToSharedStorage"));
            Assert.Contains(lines, x => x.Contains("AutomationTool executed for"));
            Assert.Contains(lines, x => x.Contains("AutomationTool exiting with ExitCode=0 (Success)"));
        }
    }
}