using System.CommandLine;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterOptions
    {
        public Option<bool> Controller = new Option<bool>("--controller", "If set, force this machine to be a controller, even if an existing Kubernetes cluster exists on the network.");

        public Option<string> Node = new Option<string>("--node", "If set, join the existing Kubernetes cluster with the controller running at the specified IP address. This is optional; if you don't provide an address, any existing cluster will be detected from UDP broadcast.");

        public Option<bool> AutoUpgrade = new Option<bool>("--auto-upgrade", "If set, RKM will automatically keep itself up-to-date with the latest release on GitHub by auto-upgrading UET. If UET is upgraded on startup, RKM will automatically exit so the local service manager can start the new version. You should not enable this option unless you're willing to test the bleeding edge version of UET, which may result in cluster inoperability. This option is off by default.");

        public Option<bool> NoAutoUpgrade = new Option<bool>("--no-auto-upgrade", "If --auto-upgrade was previously passed, this turns off the auto-upgrade system. You can later run 'uet cluster start --auto-upgrade' to turn it back on again.");

        public Option<bool> NoStreamLogs = new Option<bool>("--no-stream-logs", "Do not stream logs after the cluster service starts, instead exit.");

        public Option<bool> WaitForSysprep = new Option<bool>("--wait-for-sysprep", "If set, the service will not initialize the node until sysprep has finished. This is detected by checking if 'C:\\Windows\\Panther\\UnattendGC\\setupact.log' contains the line 'OOBE completion WNF notification is already published'. This is necessary if sysprep will change the computer name.");
    }

}
