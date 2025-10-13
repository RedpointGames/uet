namespace Redpoint.ProcessExecution.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProcessExecution.Windows;
    using Redpoint.ServiceControl;
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using System.Text;
    using Xunit;

    public class TrustedInstallerTests
    {
        [Fact]
        [SupportedOSPlatform("windows6.0.6000")]
        public async Task CanExecuteAsTrustedInstallerAsync()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var isAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            Assert.SkipUnless(isAdministrator, "This test requires Administrative permissions.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProcessExecution();
            services.AddServiceControl();
            var sp = services.BuildServiceProvider();

            var executor = sp.GetRequiredService<IProcessExecutor>();
            var serviceControl = sp.GetRequiredService<IServiceControl>();

            Assert.IsType<WindowsProcessExecutor>(executor);

            await serviceControl.StartService(
                "TrustedInstaller",
                CancellationToken.None);

            var authorityPath = Path.GetTempFileName();

            var exitCode = await executor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\system32\cmd.exe",
                    Arguments =
                    [
                        "/C",
                        $@"C:\Windows\system32\whoami.exe > {authorityPath}",
                    ],
                    RunAsTrustedInstaller = true,
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None).ConfigureAwait(true);

            var authority = File.ReadAllText(authorityPath);
            Assert.Equal(0, exitCode);
            Assert.Equal("nt authority\\system", authority.Trim(), ignoreCase: true);
        }
    }
}