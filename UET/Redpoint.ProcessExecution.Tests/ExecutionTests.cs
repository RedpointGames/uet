namespace Redpoint.ProcessExecution.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.ProcessExecution.Windows;
    using System.Runtime.Versioning;
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
                CancellationToken.None).ConfigureAwait(false);
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
                CancellationToken.None).ConfigureAwait(false);
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
                CancellationToken.None).ConfigureAwait(false);
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
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(375, exitCode);
        }

        [SkippableFact(Skip = "Only fails on the build servers for some reason.")]
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
                    cts.Token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAndSeeContentsOfSystemDriveAsync()
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
                        "C: && cd \\ && dir",
                    },
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanSeeContentsOfSystemDriveAsync()
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
                        "C: && cd \\ && dir",
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAndSeeContentsAsync()
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
                        "I: && cd \\ && dir",
                    },
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            var lines = stdout.ToString().Split("\r\n");
            Assert.Contains(lines, x => x.Contains("Redpoint.ProcessExecution.Tests.dll", StringComparison.OrdinalIgnoreCase));
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAsync()
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
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapEmptyDrivesForCmdAsync()
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
                    // @note: This causes the process executor to go through the
                    // flow of setting up per-process drive mappings, but with
                    // no drive overrides present in the device lookup map.
                    PerProcessDriveMappings = new Dictionary<char, string>(),
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanStartCmdInMappedDriveAsync()
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
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                    WorkingDirectory = "I:\\"
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [SkippableFact]
        [SupportedOSPlatform("windows")]
        public async Task CanStartCmdInMappedDriveAndSeeContentsAsync()
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
                        "dir",
                    },
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                    WorkingDirectory = "I:\\"
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(0, exitCode);
            var lines = stdout.ToString().Split("\r\n");
            Assert.Contains(lines, x => x.Contains("Redpoint.ProcessExecution.Tests.dll", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CanReliablyUseProcessExecutionEnumerable()
        {
            for (var i = 0; i < 100; i++)
            {
                using var cancellationTokenSource = new CancellationTokenSource(5000);

                var services = new ServiceCollection();
                services.AddLogging();
                services.AddProcessExecution();
                var sp = services.BuildServiceProvider();

                var executor = sp.GetRequiredService<IProcessExecutor>();

                var gotStdout = false;
                var gotExitCode = false;

                await foreach (var entry in executor.ExecuteAsync(new ProcessSpecification
                {
                    FilePath = OperatingSystem.IsWindows() ? @"C:\Windows\system32\cmd.exe" : "/bin/bash",
                    Arguments = OperatingSystem.IsWindows() ? new[] { "/C", "echo", "ok" } : new[] { "-c", "echo ok" },
                }, cancellationTokenSource.Token))
                {
                    switch (entry)
                    {
                        case StandardOutputResponse r:
                            if (r.Data.Trim() == "ok")
                            {
                                gotStdout = true;
                            }
                            break;
                        case ExitCodeResponse e:
                            gotExitCode = e.ExitCode == 0;
                            break;
                    }
                }

                Assert.True(gotStdout, $"Iteration {i + 1} expected to see stdout 'ok'.");
                Assert.True(gotExitCode, $"Iteration {i + 1} expected to get exit code.");
            }
        }
    }
}