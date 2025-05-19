using System.CommandLine;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterOptions
    {
        public Option<bool> Controller = new Option<bool>("--controller", "If set, force this machine to be a controller, even if an existing Kubernetes cluster exists on the network.");
        public Option<string> Node = new Option<string>("--node", "If set, join the existing Kubernetes cluster with the controller running at the specified IP address. This is optional; if you don't provide an address, any existing cluster will be detected from UDP broadcast.");
    }

}
