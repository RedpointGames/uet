using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager.Services
{
    internal class DefaultAssetConfiguration : IAssetConfiguration
    {
        private const string _kubernetesVersion = "1.26.1";
        private const string _etcdVersion = "3.6.4";
        private const string _cniPluginsVersion = "0.8.7";
        private const string _helmVersion = "3.17.3";

        private readonly Dictionary<string, string> _kv = new Dictionary<string, string>
        {
            { "KubernetesNode:Linux", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-node-linux-amd64.tar.gz" },
            { "KubernetesNode:Windows", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-node-windows-amd64.tar.gz" },
            { "KubernetesServer:Linux", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-server-linux-amd64.tar.gz" },
            { "Etcd:Linux", $"https://github.com/etcd-io/etcd/releases/download/v{_etcdVersion}/etcd-v{_etcdVersion}-linux-amd64.tar.gz" },
            { "CniPlugins:Linux", $"https://github.com/containernetworking/plugins/releases/download/v{_cniPluginsVersion}/cni-plugins-linux-amd64-v{_cniPluginsVersion}.tgz" },
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
