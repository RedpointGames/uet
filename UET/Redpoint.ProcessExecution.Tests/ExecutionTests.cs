namespace Redpoint.ProcessExecution.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Text;
    using Xunit;

    public class ExecutionTests
    {
        [SkippableFact]
        public async Task CanExecuteCmdAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "echo test",
                    },
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);
            Assert.Equal(0, exitCode);
        }

        [SkippableFact]
        public async Task CanCaptureCmdAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var stdout = new StringBuilder();
            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "echo test",
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [SkippableFact]
        public async Task CanGetExitCodeOneFromCmdAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "exit 1",
                    },
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);
            Assert.Equal(1, exitCode);
        }

        [SkippableFact]
        public async Task CanGetExitCodeWeirdFromCmdAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "exit 375",
                    },
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);
            Assert.Equal(375, exitCode);
        }

        [SkippableFact]
        public async Task CanTerminateCmdWithTimeoutAsync()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var cts = new CancellationTokenSource(1000);
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await executor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\Windows\system32\cmd.exe",
                        Arguments = new[]
                        {
                            "/C",
                            "timeout 5 >NUL",
                        },
                    },
                    CaptureSpecification.Passthrough,
                    cts.Token);
            });
        }
    }
}