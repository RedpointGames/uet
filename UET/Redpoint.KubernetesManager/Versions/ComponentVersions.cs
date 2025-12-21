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
        public static string Containerd = "2.2.1";
        public static string Runc = "1.3.4";
        public static string Kubernetes = "1.35.0";
        public static string Etcd = "3.6.7";
        public static string CniPlugins = "1.9.0";
        public static string FlannelCniSuffix = "-flannel1";
        public static string Flannel = "0.27.4";
    }
}
