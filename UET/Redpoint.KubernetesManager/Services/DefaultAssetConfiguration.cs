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
        private const string _containerdVersion = "1.6.18";
        private const string _runcVersion = "1.1.4";
        private const string _etcdVersion = "3.5.7";
        private const string _calicoVersion = "3.25.0";
        private const string _cniPluginsVersion = "0.8.7";
        private const string _cniSdnNetworkingVersion = "0.2.0";
        private const string _coreDnsVersion = "1.10.1";

        private readonly Dictionary<string, string> _kv = new Dictionary<string, string>
        {
            { "KubernetesNode:Linux", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-node-linux-amd64.tar.gz" },
            { "KubernetesNode:Windows", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-node-windows-amd64.tar.gz" },
            { "KubernetesServer:Linux", $"https://dl.k8s.io/v{_kubernetesVersion}/kubernetes-server-linux-amd64.tar.gz" },
            { "Containerd:Linux", $"https://github.com/containerd/containerd/releases/download/v{_containerdVersion}/containerd-{_containerdVersion}-linux-amd64.tar.gz" },
            { "Containerd:Windows", $"https://github.com/containerd/containerd/releases/download/v{_containerdVersion}/containerd-{_containerdVersion}-windows-amd64.tar.gz" },
            // A temporary patched version of containerd that works on Windows 11 until the following pull request is
            // in a containerd release: https://github.com/containerd/containerd/pull/8137
            { "ContainerdForWin11:Windows", $"https://dl-public.redpoint.games/file/dl-public-redpoint-games/redpoint-containerd-for-win11-{_containerdVersion}.zip" },
            { "Runc:Linux", $"https://github.com/opencontainers/runc/releases/download/v{_runcVersion}/runc.amd64" },
            { "Etcd:Linux", $"https://github.com/etcd-io/etcd/releases/download/v{_etcdVersion}/etcd-v{_etcdVersion}-linux-amd64.tar.gz" },
            { "CalicoWindows:Windows", $"https://github.com/projectcalico/calico/releases/download/v{_calicoVersion}/calico-windows-v{_calicoVersion}.zip" },
            { "CniPlugins:Linux", $"https://github.com/containernetworking/plugins/releases/download/v{_cniPluginsVersion}/cni-plugins-linux-amd64-v{_cniPluginsVersion}.tgz" },
            { "CniPlugins:Windows", $"https://github.com/containernetworking/plugins/releases/download/v{_cniPluginsVersion}/cni-plugins-windows-amd64-v{_cniPluginsVersion}.tgz" },
            { "CniSdnNetworking:Windows", $"https://github.com/microsoft/windows-container-networking/releases/download/v{_cniSdnNetworkingVersion}/windows-container-networking-cni-amd64-v{_cniSdnNetworkingVersion}.zip" },
            { "CalicoCtl:Linux", $"https://github.com/projectcalico/calico/releases/download/v{_calicoVersion}/calicoctl-linux-amd64" },
            { "CalicoCtl:Windows", $"https://github.com/projectcalico/calico/releases/download/v{_calicoVersion}/calicoctl-windows-amd64.exe" },
            { "UbuntuWSL:Windows", $"https://wslstorestorage.blob.core.windows.net/wslblob/CanonicalGroupLimited.UbuntuonWindows_2004.2021.825.0.AppxBundle" },
            { "CoreDNS:Windows", $"https://github.com/coredns/coredns/releases/download/v{_coreDnsVersion}/coredns_{_coreDnsVersion}_windows_amd64.tgz" },
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
