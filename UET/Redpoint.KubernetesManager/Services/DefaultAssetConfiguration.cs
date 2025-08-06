using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager.Services
{
    internal class DefaultAssetConfiguration : IAssetConfiguration
    {
        private const string _helmVersion = "3.17.3";

        private readonly Dictionary<string, string> _kv = new Dictionary<string, string>
        {
            { "UbuntuWSL:Windows", $"https://wslstorestorage.blob.core.windows.net/wslblob/CanonicalGroupLimited.UbuntuonWindows_2004.2021.825.0.AppxBundle" },
            { "Helm:Linux", $"https://get.helm.sh/helm-v{_helmVersion}-linux-amd64.tar.gz" },
        };

        public string this[string key]
        {
            get
            {
                if (key.StartsWith("RKM:Downloads:", StringComparison.Ordinal))
                {
                    key = key.Substring("RKM:Downloads:".Length);
                }
                return _kv[key];
            }
        }
    }
}
