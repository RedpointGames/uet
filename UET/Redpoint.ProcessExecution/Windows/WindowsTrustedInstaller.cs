namespace Redpoint.ProcessExecution.Windows
{
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.Security;
    using global::Windows.Win32.System.Console;
    using global::Windows.Win32.System.Services;
    using global::Windows.Win32.System.Threading;
    using global::Windows.Win32.System.WindowsProgramming;
    using Redpoint.ProcessExecution.Windows.Ntdll;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics.Arm;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using PInvoke = global::Windows.Win32.PInvoke;

    [SupportedOSPlatform("windows6.0.6000")]
    internal class WindowsTrustedInstaller
    {
        internal class NtProcessSafeHandle : SafeHandle
        {
            public NtProcessSafeHandle(nint ntProcess)
                : base(nint.Zero, true)
            {
                handle = ntProcess;
            }

            public override bool IsInvalid => handle == nint.Zero;

            protected override bool ReleaseHandle()
            {
                if (handle != nint.Zero)
                {
                    NtdllPInvoke.NtClose(handle);
                    handle = nint.Zero;
                }
                return true;
            }
        }

        internal unsafe static NtProcessSafeHandle GetTrustedInstallerParentProcess()
        {
            // Get the process ID for the service.
            uint trustedInstallerProcessId = 0;
            SC_HANDLE scm = PInvoke.OpenSCManager(
                (PCWSTR)null,
                (PCWSTR)null,
                PInvoke.SC_MANAGER_CONNECT);
            if (scm.Value == 0)
            {
                // Can't open the service manager.
                throw new RunAsTrustedInstallerFailedException("Unable to access the Service Control Manager.");
            }
            try
            {
                SC_HANDLE service = PInvoke.OpenService(
                    scm,
                    "TrustedInstaller",
                    PInvoke.SERVICE_QUERY_STATUS);
                if (service.Value == 0)
                {
                    // Can't open service.
                    throw new RunAsTrustedInstallerFailedException("Unable to access the 'TrustedInstaller' service on the Service Control Manager.");
                }
                try
                {
                    byte* serviceProcessInfo = null;
                    uint serviceProcessInfoSize = 0;
                    uint serviceProcessInfoBytesNeeded = 0;
                    var querySizeResult = PInvoke.QueryServiceStatusEx(
                        service,
                        SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO,
                        serviceProcessInfo,
                        serviceProcessInfoSize,
                        &serviceProcessInfoBytesNeeded);
                    if (!querySizeResult && (WIN32_ERROR)Marshal.GetLastWin32Error() != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                    {
                        // Can't query process info size.
                        var errorMessage = (WIN32_ERROR)Marshal.GetLastWin32Error() switch
                        {
                            WIN32_ERROR.ERROR_INVALID_HANDLE => "The handle is invalid.",
                            WIN32_ERROR.ERROR_ACCESS_DENIED => "The handle does not have the SERVICE_QUERY_STATUS access right.",
                            WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER => "The buffer is too small for the SERVICE_STATUS_PROCESS structure. Nothing was written to the structure.",
                            WIN32_ERROR.ERROR_INVALID_PARAMETER => "The cbSize member of SERVICE_STATUS_PROCESS is not valid.",
                            WIN32_ERROR.ERROR_INVALID_LEVEL => "The InfoLevel parameter contains an unsupported value.",
                            WIN32_ERROR.ERROR_SHUTDOWN_IN_PROGRESS => "The system is shutting down; this function cannot be called.",
                            _ => "Unknown error."
                        };
                        throw new RunAsTrustedInstallerFailedException($"Unable to query the size needed to store the status of the 'TrustedInstaller' service; got Win32 error: {errorMessage}");
                    }

                    serviceProcessInfo = (byte*)Marshal.AllocHGlobal((int)serviceProcessInfoBytesNeeded);
                    serviceProcessInfoSize = serviceProcessInfoBytesNeeded;
                    try
                    {
                        if (!PInvoke.QueryServiceStatusEx(
                            service,
                            SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO,
                            serviceProcessInfo,
                            serviceProcessInfoSize,
                            &serviceProcessInfoBytesNeeded))
                        {
                            // Can't query service.
                            var errorMessage = (WIN32_ERROR)Marshal.GetLastWin32Error() switch
                            {
                                WIN32_ERROR.ERROR_INVALID_HANDLE => "The handle is invalid.",
                                WIN32_ERROR.ERROR_ACCESS_DENIED => "The handle does not have the SERVICE_QUERY_STATUS access right.",
                                WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER => "The buffer is too small for the SERVICE_STATUS_PROCESS structure. Nothing was written to the structure.",
                                WIN32_ERROR.ERROR_INVALID_PARAMETER => "The cbSize member of SERVICE_STATUS_PROCESS is not valid.",
                                WIN32_ERROR.ERROR_INVALID_LEVEL => "The InfoLevel parameter contains an unsupported value.",
                                WIN32_ERROR.ERROR_SHUTDOWN_IN_PROGRESS => "The system is shutting down; this function cannot be called.",
                                _ => "Unknown error."
                            };

                            throw new RunAsTrustedInstallerFailedException($"Unable to query the status of the 'TrustedInstaller' service; got Win32 error: {errorMessage}");
                        }

                        SERVICE_STATUS_PROCESS* serviceProcessInfoCasted = (SERVICE_STATUS_PROCESS*)serviceProcessInfo;
                        trustedInstallerProcessId = serviceProcessInfoCasted->dwProcessId;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal((nint)serviceProcessInfo);
                    }
                }
                finally
                {
                    PInvoke.CloseServiceHandle(service);
                }
            }
            finally
            {
                PInvoke.CloseServiceHandle(scm);
            }
            if (trustedInstallerProcessId == 0)
            {
                // Service isn't running or process ID not available.
                throw new RunAsTrustedInstallerFailedException("Unable to get the process ID of the 'TrustedInstaller' service.");
            }

            CLIENT_ID clientId;
            clientId.UniqueThread = HANDLE.Null;
            clientId.UniqueProcess = new HANDLE((nint)trustedInstallerProcessId);

            OBJECT_ATTRIBUTES objectAttributes;

            nint ntProcess = nint.Zero;
            var openProcessResult = NtdllPInvoke.NtOpenProcess(
                &ntProcess,
                ACCESS_MASK.MAXIMUM_ALLOWED,
                &objectAttributes,
                &clientId);
            if (openProcessResult != NTSTATUS.STATUS_SUCCESS)
            {
                throw new RunAsTrustedInstallerFailedException($"Unable to open the 'TrustedInstaller' process with ID {trustedInstallerProcessId}; got NTRESULT {(openProcessResult.Value):X}.");
            }

            return new NtProcessSafeHandle(ntProcess);
        }
    }
}
