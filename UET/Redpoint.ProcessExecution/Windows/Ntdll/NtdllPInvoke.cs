namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.System.Threading;

    [SupportedOSPlatform("windows")]
    internal static unsafe class NtdllPInvoke
    {
        [DllImport("ntdll.dll")]
        internal static extern unsafe NTSTATUS NtCreateDirectoryObject(
            nint* Handle,
            ACCESS_MASK DesiredAccess,
            OBJECT_ATTRIBUTES* ObjectAttributes);

        [DllImport("ntdll.dll")]
        internal static extern unsafe NTSTATUS NtSetInformationProcess(
            nint ProcessHandle,
            PROCESS_INFORMATION_CLASS ProcessInformationClass,
            void* ProcessInformation,
            int ProcessInformationSize);

        [DllImport("ntdll.dll")]
        internal static extern unsafe NTSTATUS NtCreateSymbolicLinkObject(
            nint* LinkHandle,
            ACCESS_MASK DesiredAccess,
            OBJECT_ATTRIBUTES* ObjectAttributes,
            UNICODE_STRING* LinkTarget);
    }
}
