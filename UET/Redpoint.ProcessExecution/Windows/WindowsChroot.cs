namespace Redpoint.ProcessExecution.Windows
{
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.ProcessExecution.Windows.SystemCopy;
    using System;
    using System.Collections.Generic;
    using Redpoint.Hashing;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Windows.Win32.System.Threading;
    using global::Windows.Win32.Security;
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.System.Console;
    using Microsoft.Win32.SafeHandles;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using PInvoke = global::Windows.Win32.PInvoke;
    using Redpoint.ProcessExecution.Windows.Ntdll;
    using Redpoint.IO;

    [SupportedOSPlatform("windows5.1.2600")]
    internal static class WindowsChroot
    {
        internal static unsafe WindowsChrootState SetupChrootState(IReadOnlyDictionary<char, string> perProcessDriveMappings)
        {
            foreach (var kv in perProcessDriveMappings)
            {
                if (!Path.IsPathFullyQualified(kv.Value))
                {
                    throw new InvalidOperationException($"'{kv.Value}' must be an absolute path for drive mappings.");
                }
            }

            return new WindowsChrootState()
            {
                PerProcessDriveMappings = perProcessDriveMappings,
            };
        }

        internal static unsafe void UseChrootState(WindowsChrootState chrootState, ref PROCESS_INFORMATION processInfo)
        {
            if (chrootState.HandlesToCloseOnProcessExit != null)
            {
                throw new ArgumentException("WindowsChrootState can not be reused.");
            }

            var handlesToCloseOnProcessExit = new nint[1 + chrootState.PerProcessDriveMappings.Count];

            // Create the root NT object to hold our device map.
            nint objectDirectoryHandle;
            string objectDirectoryName = $@"\??\RedpointProcMap{Hash.GuidAsHexString(Guid.NewGuid())}";
            fixed (char* objectDirectoryNamePtr = objectDirectoryName)
            {
                var objectDirectoryNameUnicode = new Ntdll.UNICODE_STRING(objectDirectoryNamePtr, objectDirectoryName.Length);
                var objectAttributes = new OBJECT_ATTRIBUTES(
                    &objectDirectoryNameUnicode,
                    OBJECT_ATTRIBUTES_FLAGS.OBJ_CASE_INSENSITIVE);

                var status = NtdllPInvoke.NtCreateDirectoryObject(
                    &objectDirectoryHandle,
                    ACCESS_MASK.DIRECTORY_ALL_ACCESS,
                    &objectAttributes);
                if (status.SeverityCode != NTSTATUS.Severity.Success)
                {
                    throw new InvalidOperationException($"Got NTSTATUS {status.Value:X} when setting up object directory for per-process drive mappings.");
                }
                handlesToCloseOnProcessExit[0] = objectDirectoryHandle;
            }

            // Apply the device map to our process.
            {
                var status = NtdllPInvoke.NtSetInformationProcess(
                    (nint)processInfo.hProcess.Value,
                    PROCESS_INFORMATION_CLASS.ProcessDeviceMap,
                    &objectDirectoryHandle,
                    sizeof(nint));
                if (status.SeverityCode != NTSTATUS.Severity.Success)
                {
                    throw new InvalidOperationException($"Got NTSTATUS {status.Value:X} when setting information process ProcessDeviceMap.");
                }
            }

            // Create the device mappings.
            var handleIndex = 1;
            foreach (var kv in chrootState.PerProcessDriveMappings)
            {
                var driveString = $"{kv.Key}:";
                var linkTarget = DosDevicePath.GetFullyQualifiedDosDevicePath(kv.Value);
                fixed (char* driveStringPtr = driveString)
                fixed (char* linkTargetPtr = linkTarget)
                {
                    var driveStringUnicode = new Ntdll.UNICODE_STRING(driveStringPtr, driveString.Length);
                    var driveObjectAttributes = new OBJECT_ATTRIBUTES(
                        &driveStringUnicode,
                        OBJECT_ATTRIBUTES_FLAGS.OBJ_CASE_INSENSITIVE,
                        objectDirectoryHandle);
                    var linkTargetUnicode = new Ntdll.UNICODE_STRING(linkTargetPtr, linkTarget.Length);

                    nint linkHandle;
                    var status = NtdllPInvoke.NtCreateSymbolicLinkObject(
                        &linkHandle,
                        ACCESS_MASK.GENERIC_ALL,
                        &driveObjectAttributes,
                        &linkTargetUnicode);
                    if (status.SeverityCode != NTSTATUS.Severity.Success)
                    {
                        throw new InvalidOperationException($"Got NTSTATUS {status.Value:X} when mapping drive '{driveString}' to '{linkTarget}'.");
                    }
                    handlesToCloseOnProcessExit[handleIndex++] = linkHandle;
                }
            }

            chrootState.HandlesToCloseOnProcessExit = handlesToCloseOnProcessExit;
        }

        internal static unsafe void CleanupChrootState(WindowsChrootState chrootState)
        {
            if (chrootState.HandlesToCloseOnProcessExit != null)
            {
                foreach (var handle in chrootState.HandlesToCloseOnProcessExit)
                {
                    if (handle != 0)
                    {
                        PInvoke.CloseHandle(new HANDLE(handle));
                    }
                }
            }
        }

        internal static unsafe void ResumeThread(ref PROCESS_INFORMATION processInfo)
        {
            if (PInvoke.ResumeThread(processInfo.hThread) == unchecked((uint)-1))
            {
                throw new InvalidOperationException($"ResumeThread failed!");
            }
        }
    }
}
