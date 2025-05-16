namespace Redpoint.KubernetesManager
{
    using Redpoint.Uet.Configuration;

    internal class RKMCommandLineArguments
    {
        private readonly IGlobalArgsProvider _globalArgsProvider;
        private Lazy<string[]> _args;

        public string[] Arguments => _args.Value;

        public RKMCommandLineArguments(
            IGlobalArgsProvider globalArgsProvider)
        {
            _globalArgsProvider = globalArgsProvider;
            _args = new Lazy<string[]>(() =>
            {
                var serviceArgsPath = OperatingSystem.IsWindows() ? @"C:\RKM\service-args" : "/opt/rkm/service-args";
                if (File.Exists(serviceArgsPath))
                {
                    var result = File.ReadAllLines(serviceArgsPath).Concat(_globalArgsProvider.GlobalArgsArray).ToArray();
                    // Once we use the service arguments once, delete it.
                    File.Delete(serviceArgsPath);
                    return result;
                }
                else
                {
                    return _globalArgsProvider.GlobalArgsArray.ToArray();
                }
            });
        }
    }
}
