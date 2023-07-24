namespace Redpoint.ProcessExecution.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using System.Text;
    using System.Text.RegularExpressions;
    using Xunit;
    using Xunit.Abstractions;

    public class PromptResponseTests
    {
        private readonly ITestOutputHelper _output;

        public PromptResponseTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public async Task TestPromptResponse()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_output);
            });
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();
            var processExecutor = sp.GetRequiredService<IProcessExecutor>();

            var promptResponse = new CaptureSpecificationPromptResponse();
            promptResponse.Add(
                new Regex("Test:"),
                stdin =>
                {
                    stdin.WriteLine("Success");
                    return Task.CompletedTask;
                });

            File.WriteAllText("test.bat", @"
set /p test=Test:
echo %test%");
            var stringBuilder = new StringBuilder();
            var exitCode = await processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "test.bat"
                    },
                },
                CaptureSpecification.CreateFromPromptResponse(promptResponse, stringBuilder),
                new CancellationTokenSource(15000).Token);

            sp.GetRequiredService<ILogger<PromptResponseTests>>().LogTrace(stringBuilder.ToString());

            Assert.Equal(0, exitCode);
            Assert.Contains("Success", stringBuilder.ToString());
        }
    }
}