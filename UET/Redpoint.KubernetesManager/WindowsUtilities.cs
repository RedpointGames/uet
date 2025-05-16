namespace Redpoint.KubernetesManager
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    internal static partial class WindowsUtilities
    {
        [LibraryImport("shell32.dll", SetLastError = true)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsUserAnAdmin();

        [LibraryImport("Kernel32.dll")]
        [SupportedOSPlatform("windows")]
        public static partial long GetTickCount64();
    }
}
