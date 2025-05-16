namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.Win32;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The Windows preflight component checks that the system is compatible with
    /// RKM, and if it is, locks the Windows Update cycle not to upgrade to 
    /// an incompatible feature version.
    /// </summary>
    internal class WindowsPreflightComponent : IComponent
    {
        private readonly ILogger<WindowsPreflightComponent> _logger;

        public WindowsPreflightComponent(ILogger<WindowsPreflightComponent> logger)
        {
            _logger = logger;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.PreflightChecks, OnPreflightChecksAsync);
            }
        }

        [SupportedOSPlatform("windows")]
        private Task OnPreflightChecksAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (Environment.OSVersion.Version.Build < 22000)
            {
                _logger.LogCritical("This version of Windows must be upgraded to exactly Build 22000 (21H2) before RKM can run.");
                context.StopOnCriticalError();
                return Task.CompletedTask;
            }

            if (Environment.OSVersion.Version.Build != 22000)
            {
                _logger.LogCritical("This version of Windows is too new; 22H2 is currently not supported due to a bug in the Windows kernel (https://github.com/microsoft/SDN/issues/563). Please downgrade to 22000 if possible or re-install Windows to use RKM on this machine.");
                context.StopOnCriticalError();
                return Task.CompletedTask;
            }

            _logger.LogInformation("Locking this machine's Windows Update to target feature release 21H2 (Build 22000) so that Windows does not automatically upgrade to an incompatible version.");
            var windowsUpdateKey = Registry.LocalMachine.CreateSubKey("SOFTWARE").CreateSubKey("Policies").CreateSubKey("Microsoft").CreateSubKey("Windows").CreateSubKey("WindowsUpdate");
            windowsUpdateKey.SetValue("ProductVersion", "Windows 11");
            windowsUpdateKey.SetValue("TargetReleaseVersion", 1);
            windowsUpdateKey.SetValue("TargetReleaseVersionInfo", "21H2");
            return Task.CompletedTask;
        }
    }
}
