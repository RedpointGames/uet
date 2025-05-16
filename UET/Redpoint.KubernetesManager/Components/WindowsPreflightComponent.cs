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
    [SupportedOSPlatform("windows")]
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

            string buildName, buildNumber;
            bool locked;
            if (Environment.OSVersion.Version.Build == 22000)
            {
                buildName = "21H2";
                buildNumber = "22000";
                locked = true;
            }
            else if (Environment.OSVersion.Version.Build < 26100)
            {
                _logger.LogCritical("This version of Windows is broken and is not supported due to a bug in the Windows kernel (https://github.com/microsoft/SDN/issues/563). Please downgrade to 22000, upgrade to at least 24H2 or re-install Windows to use RKM on this machine.");
                context.StopOnCriticalError();
                return Task.CompletedTask;
            }
            else
            {
                buildName = "24H2";
                buildNumber = "26100+";
                locked = false;
            }

            if (locked)
            {
                _logger.LogInformation($"Locking this machine's Windows Update to target feature release {buildName} (Build {buildNumber}) so that Windows does not automatically upgrade to an incompatible version.");
                var windowsUpdateKey = Registry.LocalMachine.CreateSubKey("SOFTWARE").CreateSubKey("Policies").CreateSubKey("Microsoft").CreateSubKey("Windows").CreateSubKey("WindowsUpdate");
                windowsUpdateKey.SetValue("ProductVersion", "Windows 11");
                windowsUpdateKey.SetValue("TargetReleaseVersion", 1);
                windowsUpdateKey.SetValue("TargetReleaseVersionInfo", buildName);
            }
            else
            {
                _logger.LogInformation($"Unlocking this machine's Windows Update as this version does not need to be locked.");
                var windowsUpdateKey = Registry.LocalMachine.CreateSubKey("SOFTWARE").CreateSubKey("Policies").CreateSubKey("Microsoft").CreateSubKey("Windows").CreateSubKey("WindowsUpdate");
                foreach (var valueName in new[] { "ProductVersion", "TargetReleaseVersion", "TargetReleaseVersionInfo" })
                {
                    try
                    {
                        windowsUpdateKey.DeleteValue(valueName);
                    }
                    catch
                    {
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
