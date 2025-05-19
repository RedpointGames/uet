namespace Redpoint.KubernetesManager.Services
{
    public class DefaultRkmGlobalRootProvider : IRkmGlobalRootProvider
    {
        private Lazy<string> _rkmGlobalRoot;

        public DefaultRkmGlobalRootProvider()
        {
            _rkmGlobalRoot = new Lazy<string>(GetRkmGlobalRootInternal);
        }

        private string GetRkmGlobalRootInternal()
        {
            if (OperatingSystem.IsWindows())
            {
                return @"C:\RKM";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return "/Users/Shared/RKM";
            }
            else
            {
                return "/opt/rkm";
            }
        }

        public string RkmGlobalRoot => _rkmGlobalRoot.Value;
    }
}
