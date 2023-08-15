namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    internal enum PROCESS_INFORMATION_CLASS
    {
        ProcessBasicInformation,
        ProcessQuotaLimits,
        ProcessIoCounters,
        ProcessVmCounters,
        ProcessTimes,
        ProcessBasePriority, // invalid for query
        ProcessRaisePriority, // invalid for query
        ProcessDebugPort,
        ProcessExceptionPort, // invalid for query
        ProcessAccessToken, // invalid for query
        ProcessLdtInformation,
        ProcessLdtSize, // invalid for query
        ProcessDefaultHardErrorMode,
        ProcessIoPortHandlers,          // Note: this is kernel mode only, invalid for query
        ProcessPooledUsageAndLimits,
        ProcessWorkingSetWatch,
        ProcessUserModeIOPL, // invalid class
        ProcessEnableAlignmentFaultFixup, // invalid class
        ProcessPriorityClass,
        ProcessWx86Information,
        ProcessHandleCount,
        ProcessAffinityMask, // invalid for query
        ProcessPriorityBoost,
        ProcessDeviceMap,
        ProcessSessionInformation,
        ProcessForegroundInformation, // invalid for query
        ProcessWow64Information,
        ProcessImageFileName,
        ProcessLUIDDeviceMapsEnabled,
        ProcessBreakOnTermination,
        ProcessDebugObjectHandle,
        ProcessDebugFlags, // EProcess->Flags.NoDebugInherit
        ProcessHandleTracing,
        ProcessIoPriority,
        ProcessExecuteFlags,
        ProcessTlsInformation, // invalid class
        ProcessCookie,
        ProcessImageInformation, // last available on XPSP3
        ProcessCycleTime,
        ProcessPagePriority,
        ProcessInstrumentationCallback, // invalid class
        ProcessThreadStackAllocation, // invalid class
        ProcessWorkingSetWatchEx,
        ProcessImageFileNameWin32, // buffer is a UNICODE_STRING
        ProcessImageFileMapping, // buffer is a pointer to a file handle open with SYNCHRONIZE | FILE_EXECUTE access, return value is whether the handle is the same used to start the process
        ProcessAffinityUpdateMode,
        ProcessMemoryAllocationMode,
        ProcessGroupInformation,
        ProcessTokenVirtualizationEnabled, // invalid class
        ProcessConsoleHostProcess, // retrieves the pid for the process' corresponding conhost process
        ProcessWindowInformation, // returns the windowflags and windowtitle members of the process' peb->rtl_user_process_params
        MaxProcessInfoClass // MaxProcessInfoClass should always be the last enum
    }
}
