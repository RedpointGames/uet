namespace Redpoint.Windows.HCS
{
    using Newtonsoft.Json;
    using Redpoint.Windows.HCS.v1;
    using Redpoint.Windows.HCS.v2;
    using System.Runtime.InteropServices;

    public class HCSClient
    {
        [DllImport("vmcompute.dll", CharSet = CharSet.Unicode)]
        private static extern nint HcsEnumerateComputeSystems(string query, ref nint computeSystems, ref nint result);

        [DllImport("vmcompute.dll", CharSet = CharSet.Unicode)]
        private static extern nint HcsOpenComputeSystem(string id, ref nint computeSystem, ref nint result);

        [DllImport("vmcompute.dll", CharSet = CharSet.Unicode)]
        private static extern nint HcsCloseComputeSystem(nint computeSystem);

        [DllImport("vmcompute.dll", CharSet = CharSet.Unicode)]
        private static extern nint HcsGetComputeSystemProperties(nint computeSystem, string propertyQuery, ref nint properties, ref nint result);

        public static ContainerProperties[] EnumerateComputeSystems()
        {
            nint computeSystems = 0;
            nint result = 0;
            nint hr = HcsEnumerateComputeSystems("{}", ref computeSystems, ref result);
            var computeSystemsString = Marshal.PtrToStringUni(computeSystems);
            var resultString = Marshal.PtrToStringUni(result);
            Marshal.FreeCoTaskMem(computeSystems);
            Marshal.FreeCoTaskMem(result);
            var computeSystemsJson = JsonConvert.DeserializeObject<ContainerProperties[]>(computeSystemsString!) ?? new ContainerProperties[0];
            return computeSystemsJson;
        }

        public class ComputeSystemHandle : IDisposable
        {
            internal readonly nint _handle;

            internal ComputeSystemHandle(nint handle)
            {
                _handle = handle;
            }

            public void Dispose()
            {
                nint hr = HcsCloseComputeSystem(_handle);
            }
        }

        public static ComputeSystemHandle OpenComputeSystem(string id)
        {
            nint computeSystem = 0;
            nint result = 0;
            nint hr = HcsOpenComputeSystem(id, ref computeSystem, ref result);
            var resultResults = Marshal.PtrToStringUni(result);
            Marshal.FreeCoTaskMem(result);
            return new ComputeSystemHandle(computeSystem);
        }

        public static void GetComputeSystemProperties(ComputeSystemHandle computeSystem, Service_PropertyQuery propertyQuery)
        {
            var propertyQueryString = @"{""PropertyTypes"":[""ProcessList""]}";
            nint properties = 0;
            nint result = 0;
            nint hr = HcsGetComputeSystemProperties(computeSystem._handle, propertyQueryString, ref properties, ref result);
            var propertiesString = Marshal.PtrToStringUni(properties);
            var resultString = Marshal.PtrToStringUni(result);
            Marshal.FreeCoTaskMem(properties);
            Marshal.FreeCoTaskMem(result);
        }
    }
}