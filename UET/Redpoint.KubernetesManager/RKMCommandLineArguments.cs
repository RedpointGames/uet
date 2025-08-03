namespace Redpoint.KubernetesManager
{
    using Redpoint.KubernetesManager.Services;
    using Redpoint.Uet.Configuration;
    using System.Diagnostics.CodeAnalysis;

    public class RKMCommandLineArguments
    {
        private Lazy<string[]> _args;

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "")]
        public string[] Arguments => _args.Value;

        public RKMCommandLineArguments(
            IGlobalArgsProvider globalArgsProvider,
            IRkmGlobalRootProvider rkmGlobalRootProvider)
        {
            _args = new Lazy<string[]>(() =>
            {
                var serviceArgsPath = Path.Combine(rkmGlobalRootProvider.RkmGlobalRoot, "service-args");
                if (File.Exists(serviceArgsPath))
                {
                    var result = File.ReadAllLines(serviceArgsPath).Concat(globalArgsProvider.GlobalArgsArray).ToArray();
                    // Once we use the service arguments once, delete it.
                    File.Delete(serviceArgsPath);
                    return result;
                }
                else
                {
                    return globalArgsProvider.GlobalArgsArray.ToArray();
                }
            });
        }
    }
}
