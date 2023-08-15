namespace Redpoint.ProcessExecution.Windows
{
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.ProcessExecution.Windows.SystemCopy;
    using System;
    using System.Collections.Generic;
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

    [SupportedOSPlatform("windows5.1.2600")]
    internal static class WindowsChroot
    {
        internal static unsafe WindowsChrootState SetupChrootState(IDictionary<char, string> perProcessDriveMappings)
        {
            var mappings = new List<nint>();

            string objectDirectoryName = $@"\BaseNamedObjects\RedpointProcMap{Guid.NewGuid().ToString().Replace("-", "").ToLowerInvariant()}";
            fixed (char* objectDirectoryNamePtr = objectDirectoryName)
            {
                var objectDirectoryNameUnicode = new Ntdll.UNICODE_STRING(objectDirectoryNamePtr, objectDirectoryName.Length);
                var objectAttributes = new OBJECT_ATTRIBUTES(
                    &objectDirectoryNameUnicode,
                    OBJECT_ATTRIBUTES_FLAGS.OBJ_CASE_INSENSITIVE | OBJECT_ATTRIBUTES_FLAGS.OBJ_INHERIT);

                nint objectDirectoryHandle;
                var status = NtdllPInvoke.NtCreateDirectoryObject(
                    &objectDirectoryHandle,
                    ACCESS_MASK.DIRECTORY_ALL_ACCESS,
                    &objectAttributes);
                if (status.SeverityCode != NTSTATUS.Severity.Success)
                {
                    throw new InvalidOperationException($"Got NTSTATUS {status} when setting up object directory for per-process drive mappings.");
                }
                mappings.Add(objectDirectoryHandle);

                foreach (var kv in perProcessDriveMappings)
                {
                    if (!Path.IsPathFullyQualified(kv.Value) ||
                        Path.GetPathRoot(kv.Value) == null ||
                        Path.GetPathRoot(kv.Value)!.Length != 3)
                    {
                        throw new InvalidOperationException($"'{kv.Value}' must be an absolute path for drive mappings.");
                    }

                    string dosDevice = string.Empty;
                    var driveRoot = Path.GetPathRoot(kv.Value)!.TrimEnd('\\');
                    {
                        char[] buffer = new char[PInvoke.MAX_PATH];
                        fixed (char* bufferPtr = buffer)
                        {
                            uint length = PInvoke.QueryDosDevice(driveRoot, bufferPtr, (uint)buffer.Length);
                            if (length == 0)
                            {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                            int end;
                            for (end = 0; end < buffer.Length; end++)
                            {
                                if (buffer[end] == '\0')
                                {
                                    dosDevice = new string(buffer, 0, end);
                                    break;
                                }
                            }
                        }
                    }
                    if (dosDevice == string.Empty)
                    {
                        throw new InvalidOperationException($"Unable to resolve DosDevice for path root '{driveRoot}'");
                    }

                    var driveString = $"{kv.Key}:";
                    var linkTarget = (dosDevice + '\\' + kv.Value.Substring(3)).TrimEnd('\\');
                    fixed (char* driveStringPtr = driveString)
                    fixed (char* linkTargetPtr = linkTarget)
                    {
                        var driveStringUnicode = new Ntdll.UNICODE_STRING(driveStringPtr, driveString.Length);
                        var driveObjectAttributes = new OBJECT_ATTRIBUTES(
                            &driveStringUnicode,
                            OBJECT_ATTRIBUTES_FLAGS.OBJ_CASE_INSENSITIVE | OBJECT_ATTRIBUTES_FLAGS.OBJ_INHERIT,
                            objectDirectoryHandle);
                        var linkTargetUnicode = new Ntdll.UNICODE_STRING(linkTargetPtr, linkTarget.Length);

                        nint linkHandle;
                        status = NtdllPInvoke.NtCreateSymbolicLinkObject(
                            &linkHandle,
                            (ACCESS_MASK)0xF0001,
                            &driveObjectAttributes,
                            &linkTargetUnicode);
                        if (status.SeverityCode != NTSTATUS.Severity.Success)
                        {
                            throw new InvalidOperationException($"Got NTSTATUS {status} when mapping drive '{driveString}' to '{linkTarget}'.");
                        }
                        mappings.Add(linkHandle);
                    }
                }

                return new WindowsChrootState
                {
                    Handles = mappings.ToArray(),
                    ObjectRootHandle = objectDirectoryHandle,
                };
            }
        }

        internal static unsafe void ApplyChrootStateToExistingProcess(WindowsChrootState chrootState, int pid)
        {
            var p = Process.GetProcessById(pid);

            nint objectRootHandle = chrootState.ObjectRootHandle;
            var status = NtdllPInvoke.NtSetInformationProcess(
                p.Handle,
                PROCESS_INFORMATION_CLASS.ProcessDeviceMap,
                &objectRootHandle,
                sizeof(nint));
            if (status.SeverityCode != NTSTATUS.Severity.Success)
            {
                throw new InvalidOperationException($"Got NTSTATUS {status} when setting information process ProcessDeviceMap.");
            }
        }

        internal static unsafe void UseChrootStateAndResumeThread(WindowsChrootState chrootState, ref PROCESS_INFORMATION processInfo)
        {
            nint objectRootHandle = chrootState.ObjectRootHandle;
            var status = NtdllPInvoke.NtSetInformationProcess(
                processInfo.hProcess.Value,
                PROCESS_INFORMATION_CLASS.ProcessDeviceMap,
                &objectRootHandle,
                sizeof(nint));
            if (status.SeverityCode != NTSTATUS.Severity.Success)
            {
                throw new InvalidOperationException($"Got NTSTATUS {status} when setting information process ProcessDeviceMap.");
            }

            foreach (var mapping in chrootState.Handles)
            {
                PInvoke.CloseHandle(new HANDLE(mapping));
            }

            if (PInvoke.ResumeThread(processInfo.hThread) == unchecked((uint)-1))
            {
                throw new InvalidOperationException($"ResumeThread failed!");
            }
        }
    }
}
