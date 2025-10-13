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
        [Fact]
        public async Task CanExecuteCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments =
                    [
                        "/C",
                        "echo test",
                    ],
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task CanCaptureCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "echo test",
                    ],
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [Fact]
        public async Task CanGetExitCodeOneFromCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments =
                    [
                        "/C",
                        "exit 1",
                    ],
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task CanGetExitCodeWeirdFromCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments =
                    [
                        "/C",
                        "exit 375",
                    ],
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(375, exitCode);
        }

        [Fact(Skip = "Only fails on the build servers for some reason.")]
        public async Task CanTerminateCmdWithTimeoutAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                        Arguments =
                        [
                            "/C",
                            "timeout 5 >NUL",
                        ],
                    },
                    CaptureSpecification.Passthrough,
                    cts.Token).ConfigureAwait(false);
            }).ConfigureAwait(true);
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAndSeeContentsOfSystemDriveAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "C: && cd \\ && dir",
                    ],
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanSeeContentsOfSystemDriveAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "C: && cd \\ && dir",
                    ],
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAndSeeContentsAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "I: && cd \\ && dir",
                    ],
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
            var lines = stdout.ToString().Split("\r\n");
            Assert.Contains(lines, x => x.Contains("Redpoint.ProcessExecution.Tests.dll", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapDriveForCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "echo test",
                    ],
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanMapEmptyDrivesForCmdAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "echo test",
                    ],
                    // @note: This causes the process executor to go through the
                    // flow of setting up per-process drive mappings, but with
                    // no drive overrides present in the device lookup map.
                    PerProcessDriveMappings = new Dictionary<char, string>(),
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanStartCmdInMappedDriveAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "echo test",
                    ],
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                    WorkingDirectory = "I:\\"
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(0, exitCode);
            Assert.Equal("test", stdout.ToString().Trim());
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanStartCmdInMappedDriveAndSeeContentsAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

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
                    Arguments =
                    [
                        "/C",
                        "dir",
                    ],
                    PerProcessDriveMappings = new Dictionary<char, string>
                    {
                        { 'I', Environment.CurrentDirectory }
                    },
                    WorkingDirectory = "I:\\"
                },
                CaptureSpecification.CreateFromStdoutStringBuilder(stdout),
                CancellationToken.None).ConfigureAwait(true);
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
                    Arguments = OperatingSystem.IsWindows() ? new LogicalProcessArgument[] { "/C", "echo", "ok" } : ["-c", "echo ok"],
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