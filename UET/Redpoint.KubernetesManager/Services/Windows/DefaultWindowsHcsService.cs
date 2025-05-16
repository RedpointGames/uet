namespace Redpoint.KubernetesManager.Implementations
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Models.Hcs;
    using Redpoint.KubernetesManager.Services.Windows;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal partial class DefaultWindowsHcsService : IWindowsHcsService
    {

        [LibraryImport("vmcompute.dll", StringMarshalling = StringMarshalling.Utf16)]
        [SupportedOSPlatform("windows")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int HcsEnumerateComputeSystems(string query, [MarshalAs(UnmanagedType.LPWStr)] out string computeSystems, [MarshalAs(UnmanagedType.LPWStr)] out string result);

        [LibraryImport("vmcompute.dll", StringMarshalling = StringMarshalling.Utf16)]
        [SupportedOSPlatform("windows")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int HcsOpenComputeSystem(string id, out IntPtr computeSystem, [MarshalAs(UnmanagedType.LPWStr)] out string result);

        [LibraryImport("vmcompute.dll", StringMarshalling = StringMarshalling.Utf16)]
        [SupportedOSPlatform("windows")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int HcsCloseComputeSystem(IntPtr computeSystem);

        [LibraryImport("vmcompute.dll", StringMarshalling = StringMarshalling.Utf16)]
        [SupportedOSPlatform("windows")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int HcsTerminateComputeSystem(IntPtr computeSystem, string? options, [MarshalAs(UnmanagedType.LPWStr)] out string result);

        public HcsComputeSystemWithId[] GetHcsComputeSystems()
        {
            if (HcsEnumerateComputeSystems(string.Empty, out string computeSystems, out _) == 0)
            {
                return JsonSerializer.Deserialize(computeSystems, WindowsHostJsonSerializerContext.Default.HcsComputeSystemWithIdArray)!;
            }
            return [];
        }

        public void TerminateHcsSystem(string id)
        {
            if (HcsOpenComputeSystem(id, out nint computeSystem, out _) == 0)
            {
                HcsTerminateComputeSystem(computeSystem, null, out _);
                _ = HcsCloseComputeSystem(computeSystem);
            }
        }
    }
}
