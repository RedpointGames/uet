namespace Redpoint.Uet.Uat.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.OpenGE.ProcessExecution;
    using Redpoint.OpenGE.Executor;
    using Redpoint.Uet.Core;
    using Redpoint.GrpcPipes;

    public class UATExecutionTests
    {
        [SkippableFact()]
        public async Task TestUATExecutionOfBuildGraphHelpWorks()
        {
            var enginePath = Environment.GetEnvironmentVariable("UET_ENGINE_PATH") ?? @"E:\EpicGames\UE_5.2";
            Skip.IfNot(Directory.Exists(enginePath), $"Engine must exist at {enginePath} for this test to run.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddOpenGEExecutor();
            services.AddOpenGEProcessExecution();
            services.AddGrpcPipes();
            services.AddUETCore();
            services.AddUETUAT();

            var serviceProvider = services.BuildServiceProvider();
            var executor = serviceProvider.GetRequiredService<IUATExecutor>();

            var lines = new List<string>();
            var exitCode = await executor.ExecuteAsync(
                enginePath,
                new UATSpecification
                {
                    Command = "BuildGraph",
                    Arguments = new[] { "-help" },
                },
                CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates()
                {
                    ReceiveStdout = (line) =>
                    {
                        lines.Add(line);
                        return false;
                    }
                }),
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains(lines, x => x.Contains("-WriteToSharedStorage"));
            Assert.Contains(lines, x => x.Contains("AutomationTool executed for"));
            Assert.Contains(lines, x => x.Contains("AutomationTool exiting with ExitCode=0 (Success)"));
        }
    }
}