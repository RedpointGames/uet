namespace uet.FunctionalTests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Collections;
    using System.Reflection;
    using System.Text.Json;
    using Xunit.Abstractions;
    using Redpoint.ProcessExecution;

    public class Functional
    {
        private readonly ITestOutputHelper _output;

        public Functional(ITestOutputHelper output)
        {
            _output = output;
        }

        public class FunctionalTestEntry
        {
            public required FunctionalTestConfig Config { get; init; }

            public required string Name { get; init; }

            public required string Path { get; init; }

            public override string ToString()
            {
                return Name;
            }
        }

        public class FunctionalTestTheoryGenerator : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var dir in Directory.GetDirectories(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "Tests")))
                {
                    var configPath = Path.Combine(dir, "FunctionalTestConfig.json");
                    if (File.Exists(configPath))
                    {
                        var config = JsonSerializer.Deserialize<FunctionalTestConfig[]>(File.ReadAllText(configPath))!;
                        foreach (var entry in config)
                        {
                            yield return new object[]
                            {
                                new FunctionalTestEntry
                                {
                                    Config = entry,
                                    Name = Path.GetFileName(dir) + "." + entry.Name,
                                    Path = dir,
                                }
                            };
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [SkippableTheory]
        [ClassData(typeof(FunctionalTestTheoryGenerator))]
        public async Task Uet(FunctionalTestEntry test)
        {
            Skip.If(Environment.GetEnvironmentVariable("CI") == "true", "Functional tests can not be run on a build server and are currently for interactive testing only");
            Skip.IfNot(OperatingSystem.IsWindows(), "Functional tests must be run from Windows");

            var path = test.Config!.Type switch
            {
                "uet" => Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "uet.exe")),
                "shim" => Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "uet.shim.exe")),
                _ => throw new NotSupportedException()
            };

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddXUnit(_output);
            });
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();
            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = path,
                    Arguments = test.Config.Arguments ?? Array.Empty<string>(),
                    WorkingDirectory = test.Path,
                },
                CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                {
                    ReceiveStdout = (line) => { _output.WriteLine(line); return false; },
                    ReceiveStderr = (line) => { _output.WriteLine(line); return false; },
                }),
                CancellationToken.None);
            Assert.Equal(0, exitCode);
        }
    }
}