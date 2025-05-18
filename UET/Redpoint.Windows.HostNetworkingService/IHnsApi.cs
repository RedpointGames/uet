namespace Redpoint.Windows.HostNetworkingService
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    public interface IHnsApi
    {
        HnsNetworkWithId[] GetHnsNetworks();

        void NewHnsNetwork(HnsNetwork network);

        void DeleteHnsNetwork(string id);

        HnsEndpointWithId[] GetHnsEndpoints();

        HnsPolicyListWithId[] GetHnsPolicyLists();

        void DeleteHnsPolicyList(string id);

        private static IHnsApi? _instance;

        public static IHnsApi GetInstance()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException();
            }

            if (_instance == null)
            {
                _instance = new DefaultHns();
            }
            return _instance;
        }
    }
}
