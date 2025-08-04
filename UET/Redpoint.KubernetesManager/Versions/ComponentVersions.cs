namespace Redpoint.KubernetesManager.Versions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    // @note: This will move to configs in the future.
    internal class ComponentVersions
    {
        public static string Containerd = "2.1.4";
        public static string Runc = "1.3.0";
        public static string Kubernetes = "1.33.3";
        public static string Etcd = "3.6.4";
        public static string Calico = "3.30.2";
        public static string Cni = "0.8.7";
        public static string Sdn = "0.2.0";
    }
}
