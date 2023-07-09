namespace uet.FunctionalTests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Collections;
    using System.Reflection;
    using Xunit.Abstractions;
    using Redpoint.ProcessExecution;
    using System.Text;
    using Redpoint.Uet.Core;

    public class Functional
    {
        private readonly ITestOutputHelper _output;

        public Functional(ITestOutputHelper output)
        {
            _output = output;
        }

        [Functional]
        public async Task Uet(FunctionalTestEntry test)
        {
            Skip.IfNot(OperatingSystem.IsWindows(), "Functional tests must be run from Windows");

            var path = test.Config!.Type switch
            {
                "uet" => Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "uet.exe")),
                "shim" => Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "uet.shim.exe")),
                _ => throw new NotSupportedException()
            };

            const string engine = "uefs:registry.redpoint.games/redpointgames/infrastructure/unreal-engine-epic:5.2";

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddXUnit(_output);
            });
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var targetPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", test.Name);
            await DirectoryAsync.CopyAsync(test.Path, targetPath, true);

            var output = new StringBuilder();
            var executor = sp.GetRequiredService<IProcessExecutor>();
            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = path,
                    Arguments = (test.Config.Arguments ?? Array.Empty<string>())
                        .Select(x => x.Replace("{ENGINE}", engine))
                        .ToArray(),
                    WorkingDirectory = targetPath,
                },
                CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                {
                    ReceiveStdout = line =>
                    {
                        _output.WriteLine(line);
                        return false;
                    },
                    ReceiveStderr = (line) => { _output.WriteLine(line); return false; },
                }),
                CancellationToken.None);
            Assert.Equal(0, exitCode);
            if (test.Config.OutputMustContain != null)
            {
                Assert.Contains(test.Config.OutputMustContain, output.ToString());
            }
        }
    }
}