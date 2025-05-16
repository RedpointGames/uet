namespace Redpoint.Windows.Firewall
{
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    public interface IWindowsFirewall
    {
        void UpsertPortRule(
            string name,
            bool allow,
            int port,
            Protocol protocol);

        void UpsertApplicationRule(
            string name,
            bool allow,
            string path);

        private static IWindowsFirewall? _instance;

        public static IWindowsFirewall GetInstance()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException();
            }

            if (_instance == null)
            {
                _instance = new DefaultWindowsFirewall();
            }
            return _instance;
        }
    }
}
