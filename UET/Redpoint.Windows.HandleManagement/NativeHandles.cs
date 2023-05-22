using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.Windows.HandleManagement.Tests")]

namespace Redpoint.Windows.HandleManagement
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using global::Windows.Win32;
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.System.WindowsProgramming;
    using Redpoint.Windows.VolumeManagement;
    using Redpoint.Collections;
    using System.Linq;

    /// <summary>
    /// Static API methods for querying and closing handles on Windows.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public static class NativeHandles
    {
#pragma warning disable IDE1006 // Naming Styles
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SYSTEM_HANDLE
        {
            [MarshalAs(UnmanagedType.U4)]
            internal uint ProcessId;

            [MarshalAs(UnmanagedType.U1)]
            internal byte ObjectTypeNumber;

            [MarshalAs(UnmanagedType.U1)]
            internal byte Flags;

            [MarshalAs(UnmanagedType.U2)]
            internal ushort Handle;

            internal void* Object;

            [MarshalAs(UnmanagedType.U4)]
            internal uint GrantedAccess;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SYSTEM_HANDLE_INFORMATION
        {
            internal uint HandleCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct GENERIC_MAPPING
        {
            internal uint GenericRead;
            internal uint GenericWrite;
            internal uint GenericExecute;
            internal uint GenericAll;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct OBJECT_TYPE_INFORMATION
        {
            internal UNICODE_STRING Name;
            internal uint TotalNumberOfObjects;
            internal uint TotalNumberOfHandles;
            internal uint TotalPagedPoolUsage;
            internal uint TotalNonPagedPoolUsage;
            internal uint TotalNamePoolUsage;
            internal uint TotalHandleTableUsage;
            internal uint HighWaterNumberOfObjects;
            internal uint HighWaterNumberOfHandles;
            internal uint HighWaterPagedPoolUsage;
            internal uint HighWaterNonPagedPoolUsage;
            internal uint HighWaterNamePoolUsage;
            internal uint HighWaterHandleTableUsage;
            internal uint InvalidAttributes;
            internal GENERIC_MAPPING GenericMapping;
            internal uint ValidAccess;
            internal bool SecurityRequired;
            internal bool MaintainHandleCount;
            internal ushort MaintainTypeList;
            internal uint PoolType;
            internal uint PagedPoolUsage;
            internal uint NonPagedPoolUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct QueryStructure
        {
            internal nint Handle;
            internal void* ObjectNameInfo;
            internal uint ObjectInfoLength;
            internal uint ReturnLength;
            internal int Result;
        }

        [DllImport("ntdll.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern unsafe NTSTATUS NtDuplicateObject(HANDLE SourceProcessHandle, HANDLE SourceHandle, HANDLE TargetProcessHandle, nint* TargetHandle, uint DesiredAccess, uint Attributes, uint Options);

#pragma warning restore IDE1006 // Naming Styles

        private const SYSTEM_INFORMATION_CLASS _systemHandleInformation = (SYSTEM_INFORMATION_CLASS)16;
        private const nint _sizeofSYSTEM_HANDLE_INFORMATION = 8;

        internal static SYSTEM_HANDLE[] GetSystemHandles()
        {
            unsafe
            {
                unchecked
                {
                    uint returnLength = 0;
                    NTSTATUS status = PInvoke.NtQuerySystemInformation(
                        _systemHandleInformation,
                        null,
                        0,
                        ref returnLength);
                    if (status.Value != 0x0 &&
                        status.Value != (int)0xc0000004u)
                    {
                        throw new NTSTATUSException(status);
                    }
                    while (status.Value == (int)0xc0000004u)
                    {
                        nint ptr = Marshal.AllocHGlobal((int)returnLength);
                        var resultHandles = new List<SYSTEM_HANDLE>();
                        try
                        {
                            status = PInvoke.NtQuerySystemInformation(
                                _systemHandleInformation,
                                (void*)ptr,
                                returnLength,
                                ref returnLength);
                            if (status.Value != 0x0 &&
                                status.Value != (int)0xc0000004u)
                            {
                                throw new NTSTATUSException(status);
                            }
                            if (status.Value == (int)0xc0000004u)
                            {
                                continue;
                            }

                            var handleInfo = Marshal.PtrToStructure<SYSTEM_HANDLE_INFORMATION>(ptr);
                            nint handleStart = ptr + _sizeofSYSTEM_HANDLE_INFORMATION;
                            for (nint i = 0; i < handleInfo.HandleCount; i++)
                            {
                                var handlePtr = handleStart + i * sizeof(SYSTEM_HANDLE);
                                var systemHandle = Marshal.PtrToStructure<SYSTEM_HANDLE>(handlePtr);
                                resultHandles.Add(systemHandle);
                            }
                            return resultHandles.ToArray();
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
            }
            throw new InvalidOperationException();
        }

        internal enum GetPathResultCode
        {
            Success,
            ExpiredHandle,
            ObjectPathInvalid,
            Unsuccessful,
            NotSupported,
            AccessDenied,
            Cancelled,
            PipeDisconnected
        }

        private static readonly Lazy<Dictionary<string, string[]>> _deviceMappings = new Lazy<Dictionary<string, string[]>>(() =>
        {
            var volumeMappings = new Dictionary<string, string[]>();
            foreach (var systemVolume in new SystemVolumes())
            {
                volumeMappings.Add(systemVolume.DeviceName.TrimEnd('\\'), systemVolume.VolumePathNames.Select(x => x.TrimEnd('\\')).ToArray());
            }
            return volumeMappings;
        });

        internal static GetPathResultCode GetPathForHandle(SYSTEM_HANDLE targetHandle, bool resolveFilePath, ref string? objectPath, ref string[]? filePaths)
        {
            HANDLE targetProcessHandle =
            PInvoke.OpenProcess(global::Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, false, targetHandle.ProcessId);
            if (targetProcessHandle.IsNull)
            {
                var lastError = Marshal.GetLastWin32Error();
                if (
                    // AccessDenied
                    lastError == 5 ||
                    // InvalidParameter
                    // @note: this is a lie, but any process we can't access is basically AccessDenied
                    lastError == 87)
                {
                    return GetPathResultCode.AccessDenied;
                }
                throw new Win32Exception(lastError);
            }
            try
            {
                nint duplicatedHandle = new nint(0);
                NTSTATUS status;
                unsafe
                {
                    status = NtDuplicateObject(
                        targetProcessHandle,
                        (HANDLE)targetHandle.Handle,
                        PInvoke.GetCurrentProcess(),
                        &duplicatedHandle,
                        0, 0, 0);
                }
                if (status != 0x0)
                {
                    switch (unchecked((uint)status.Value))
                    {
                        case NTSTATUSException.NT_STATUS_INVALID_HANDLE:
                        case NTSTATUSException.NT_STATUS_VOLUME_DISMOUNTED:
                            return GetPathResultCode.ExpiredHandle;
                        case NTSTATUSException.NT_STATUS_OBJECT_PATH_INVALID:
                            return GetPathResultCode.ObjectPathInvalid;
                        case NTSTATUSException.NT_STATUS_NOT_SUPPORTED:
                            return GetPathResultCode.NotSupported;
                        case NTSTATUSException.NT_STATUS_UNSUCCESSFUL:
                            return GetPathResultCode.Unsuccessful;
                        case NTSTATUSException.NT_STATUS_ACCESS_DENIED:
                        case NTSTATUSException.NT_STATUS_PROCESS_IS_TERMINATING:
                            return GetPathResultCode.AccessDenied;
                        case NTSTATUSException.NT_STATUS_CANCELLED:
                            return GetPathResultCode.Cancelled;
                        case NTSTATUSException.NT_STATUS_PIPE_DISCONNECTED:
                            return GetPathResultCode.PipeDisconnected;
                    }
                    throw new NTSTATUSException(status);
                }
                try
                {
                    unsafe
                    {
                        unchecked
                        {
                            uint returnLength = 0;
                            status = NtQueryObjectPool.Instance.NtQueryObject(
                                targetHandle.ProcessId,
                                (HANDLE)duplicatedHandle,
                                (OBJECT_INFORMATION_CLASS)1,
                                0x0,
                                0,
                                ref returnLength);
                            if (status.Value != 0x0 &&
                                status.Value != (int)0xc0000004u)
                            {
                                switch (unchecked((uint)status.Value))
                                {
                                    case NTSTATUSException.NT_STATUS_INVALID_HANDLE:
                                    case NTSTATUSException.NT_STATUS_VOLUME_DISMOUNTED:
                                        return GetPathResultCode.ExpiredHandle;
                                    case NTSTATUSException.NT_STATUS_OBJECT_PATH_INVALID:
                                        return GetPathResultCode.ObjectPathInvalid;
                                    case NTSTATUSException.NT_STATUS_NOT_SUPPORTED:
                                        return GetPathResultCode.NotSupported;
                                    case NTSTATUSException.NT_STATUS_UNSUCCESSFUL:
                                        return GetPathResultCode.Unsuccessful;
                                    case NTSTATUSException.NT_STATUS_ACCESS_DENIED:
                                    case NTSTATUSException.NT_STATUS_PROCESS_IS_TERMINATING:
                                        return GetPathResultCode.AccessDenied;
                                    case NTSTATUSException.NT_STATUS_CANCELLED:
                                        return GetPathResultCode.Cancelled;
                                    case NTSTATUSException.NT_STATUS_PIPE_DISCONNECTED:
                                        return GetPathResultCode.PipeDisconnected;
                                }
                                throw new NTSTATUSException(status);
                            }
                            while (status.Value == (int)0xc0000004u)
                            {
                                nint ptr = Marshal.AllocHGlobal((int)returnLength);
                                try
                                {
                                    status = NtQueryObjectPool.Instance.NtQueryObject(
                                        targetHandle.ProcessId,
                                        (HANDLE)duplicatedHandle,
                                        (OBJECT_INFORMATION_CLASS)1,
                                        ptr,
                                        returnLength,
                                        ref returnLength);
                                    if (status.Value != 0x0 &&
                                        status.Value != (int)0xc0000004u)
                                    {
                                        switch (unchecked((uint)status.Value))
                                        {
                                            case NTSTATUSException.NT_STATUS_INVALID_HANDLE:
                                            case NTSTATUSException.NT_STATUS_VOLUME_DISMOUNTED:
                                                return GetPathResultCode.ExpiredHandle;
                                            case NTSTATUSException.NT_STATUS_OBJECT_PATH_INVALID:
                                                return GetPathResultCode.ObjectPathInvalid;
                                            case NTSTATUSException.NT_STATUS_NOT_SUPPORTED:
                                                return GetPathResultCode.NotSupported;
                                            case NTSTATUSException.NT_STATUS_UNSUCCESSFUL:
                                                return GetPathResultCode.Unsuccessful;
                                            case NTSTATUSException.NT_STATUS_ACCESS_DENIED:
                                            case NTSTATUSException.NT_STATUS_PROCESS_IS_TERMINATING:
                                                return GetPathResultCode.AccessDenied;
                                            case NTSTATUSException.NT_STATUS_CANCELLED:
                                                return GetPathResultCode.Cancelled;
                                            case NTSTATUSException.NT_STATUS_PIPE_DISCONNECTED:
                                                return GetPathResultCode.PipeDisconnected;
                                        }
                                        throw new NTSTATUSException(status);
                                    }
                                    if (status.Value == (int)0xc0000004u)
                                    {
                                        continue;
                                    }

                                    var unicodeString = Marshal.PtrToStructure<UNICODE_STRING>(ptr);
                                    var objectPathLocal = new string(unicodeString.Buffer, 0, unicodeString.Length / 2);
                                    objectPath = objectPathLocal;

                                    if (resolveFilePath)
                                    {
                                        foreach (var kv in _deviceMappings.Value)
                                        {
                                            if (objectPathLocal.StartsWith(kv.Key, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                filePaths = kv.Value.Select(x => x + objectPathLocal.Substring(kv.Key.Length)).ToArray();
                                                return GetPathResultCode.Success;
                                            }
                                        }
                                        filePaths = new string[0];
                                        return GetPathResultCode.Success;
                                    }
                                    else
                                    {
                                        return GetPathResultCode.Success;
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeHGlobal(ptr);
                                }
                            }
                        }
                    }
                    return GetPathResultCode.Unsuccessful;
                }
                finally
                {
                    PInvoke.CloseHandle((HANDLE)duplicatedHandle);
                }
            }
            finally
            {
                PInvoke.CloseHandle(targetProcessHandle);
            }
        }

        /// <summary>
        /// Query all of the open handles on the system.
        /// </summary>
        /// <remarks>
        /// This function returns all types of handles, not just files. If you want to query for just file handles,
        /// use <see cref="GetAllFileHandlesAsync(CancellationToken)"/> instead, as this will also translate object
        /// paths to file paths for you.
        /// </remarks>
        /// <param name="cancellationToken">The cancellation token to interrupt the query.</param>
        /// <returns>An asynchronous enumerable of raw handles.</returns>
        public static IAsyncEnumerable<RawNativeHandle> GetAllHandlesAsync(CancellationToken cancellationToken)
        {
            var handles = GetSystemHandles();

            return handles.GroupBy(x => x.ProcessId)
                .ToAsyncEnumerable()
                .SelectFastAwait(async handleGroup =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await Task.Run(() =>
                    {
                        var results = new List<RawNativeHandle>();
                        foreach (var handle in handleGroup)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string? objectPath = null;
                            string[]? filePaths = null;
                            var result = GetPathForHandle(handle, false, ref objectPath, ref filePaths);
                            if (result == GetPathResultCode.AccessDenied)
                            {
                                // We can't access handles in the target process.
                                //return new List<RawNativeHandle>();
                            }
                            else if (result == GetPathResultCode.Success)
                            {
                                results.Add(new RawNativeHandle(handle, objectPath!));
                            }
                        }
                        return results;
                    });
                })
                .SelectMany(x => x.ToAsyncEnumerable());
        }

        /// <summary>
        /// Query all of the open file handles on the system.
        /// </summary>
        /// <remarks>
        /// This function returns all of the open file handles. You can compare <see cref="FileNativeHandle.FilePath"/>
        /// against the file you want to access to see which handles to forcibly close.
        /// </remarks>
        /// <param name="cancellationToken">The cancellation token to interrupt the query.</param>
        /// <returns>An asynchronous enumerable of file handles.</returns>
        public static IAsyncEnumerable<FileNativeHandle> GetAllFileHandlesAsync(CancellationToken cancellationToken)
        {
            var handles = GetSystemHandles();

            return handles.GroupBy(x => x.ProcessId)
                .ToAsyncEnumerable()
                .SelectFastAwait(async handleGroup =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await Task.Run(() =>
                    {
                        var results = new List<FileNativeHandle>();
                        foreach (var handle in handleGroup)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string? objectPath = null;
                            string[]? filePaths = null;
                            var result = GetPathForHandle(handle, true, ref objectPath, ref filePaths);
                            if (result == GetPathResultCode.AccessDenied)
                            {
                                // We can't access handles in the target process.
                                //return new List<FileNativeHandle>();
                            }
                            else if (result == GetPathResultCode.Success)
                            {
                                foreach (var filePath in filePaths!)
                                {
                                    results.Add(new FileNativeHandle(handle, filePath));
                                }
                            }
                        }
                        return results;
                    });
                })
                .SelectMany(x => x.ToAsyncEnumerable());
        }

        /// <summary>
        /// Forcibly closes an open handle. You can use this function to forcibly unlock files by 
        /// closing all open handles that point to a specific file.
        /// </summary>
        /// <remarks>
        /// This function can be used to close more than just files. You can also pass in raw handles to close them.
        /// </remarks>
        /// <param name="nativeHandle">The raw handle or file handle to forcibly close.</param>
        /// <param name="cancellationToken">The cancellation token to interrupt the close operation.</param>
        /// <returns>An asynchronous task that can be awaited.</returns>
        /// <exception cref="Win32Exception">Thrown if the process that opens the file can't be accessed.</exception>
        /// <exception cref="NTSTATUSException">Thrown if the handle can not be forcibly closed.</exception>
        public static Task ForciblyCloseHandleAsync(INativeHandle nativeHandle, CancellationToken cancellationToken)
        {
            INativeHandleInternal nativeHandleInternal = (INativeHandleInternal)nativeHandle;

            HANDLE targetProcessHandle =
            PInvoke.OpenProcess(global::Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, false, nativeHandleInternal.Handle.ProcessId);
            if (targetProcessHandle.IsNull)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try
            {
                unsafe
                {
                    NTSTATUS status = NtDuplicateObject(
                        targetProcessHandle,
                        (HANDLE)nativeHandleInternal.Handle.Handle,
                        (HANDLE)0,
                        (nint*)(nint)0,
                        0,
                        0,
                        1);
                    if (status != 0x0)
                    {
                        throw new NTSTATUSException(status);
                    }
                }
            }
            finally
            {
                PInvoke.CloseHandle(targetProcessHandle);
            }

            return Task.CompletedTask;
        }
    }
}
