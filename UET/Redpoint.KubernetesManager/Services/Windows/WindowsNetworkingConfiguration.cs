namespace Redpoint.KubernetesManager.Services.Windows
{
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;
    using System.Resources;
    using System.Text;
    using Redpoint.KubernetesManager.Services;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Hosting;
    using System.Globalization;
    using Redpoint.Windows.HostNetworkingService;
    using Redpoint.Windows.Firewall;
    using Redpoint.Registry;
    using Redpoint.ServiceControl;

    [SupportedOSPlatform("windows")]
    internal class WindowsNetworkingConfiguration : INetworkingConfiguration
    {
        private readonly ILogger<WindowsNetworkingConfiguration> _logger;
        private readonly IHnsApi _hnsService;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IWslDistro _wslDistro;
        private readonly IWslTranslation _wslTranslation;
        private readonly IPathProvider _pathProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IResourceManager _resourceManager;
        private readonly IWindowsFirewall _windowsFirewall;
        private readonly IServiceControl _serviceControl;

        public WindowsNetworkingConfiguration(
            ILogger<WindowsNetworkingConfiguration> logger,
            IHnsApi hnsService,
            ILocalEthernetInfo localEthernetInfo,
            IHostApplicationLifetime hostApplicationLifetime,
            IWslDistro wslDistro,
            IWslTranslation wslTranslation,
            IPathProvider pathProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IResourceManager resourceManager,
            IWindowsFirewall windowsFirewall,
            IServiceControl serviceControl)
        {
            _logger = logger;
            _hnsService = hnsService;
            _localEthernetInfo = localEthernetInfo;
            _hostApplicationLifetime = hostApplicationLifetime;
            _wslDistro = wslDistro;
            _wslTranslation = wslTranslation;
            _pathProvider = pathProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _resourceManager = resourceManager;
            _windowsFirewall = windowsFirewall;
            _serviceControl = serviceControl;
        }

        private bool IsInCIDR(IPAddress address, string subnetCIDR)
        {
            var subnetCIDRSplit = _localEthernetInfo.HostSubnetCIDR!.Split('/');
            var maskBits = BitConverter.ToUInt32(IPAddress.Parse(subnetCIDRSplit[0]).GetAddressBytes().Reverse().ToArray(), 0);
            var wslBits = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0);
            var mask = uint.MaxValue << 32 - int.Parse(subnetCIDRSplit[1], CultureInfo.InvariantCulture);
            return (maskBits & mask) == (wslBits & mask);
        }

        /// <summary>
        /// Unblocks a VFP port by MAC address, allowing it to bypass the overlay network
        /// and attach directly to the external network.
        /// </summary>
        /// <remarks>
        /// vfpctrl.exe is completely undocumented (well, it has extensive command help with /?,
        /// but it's otherwise not documented on the Internet).
        /// 
        /// This tool allows us to control the VFP extension attached to the External switch. From
        /// what I can gather, the Hyper-V switches work a little bit like this:
        /// 
        /// | VM 1      | -> (port) /---------------------\
        /// | VM 2      | -> (port) | Hyper-V Switch      | 
        /// | WSL       | -> (port) |   /---------------\ |
        /// | Container | -> (port) | > | VFP Extension | | -> External Network
        /// | Container | -> (port) |   \---------------/ | 
        /// | Container | -> (port) \---------------------/
        /// 
        /// Everything gets represented as "ports" on the switch, and the VFP extension then (by
        /// default when an overlay network is present) intercepts all traffic to make the overlay
        /// network work.
        /// 
        /// An unconfigured port on the switch becomes blocked by default when an overlay network
        /// is present, because there's been no policies set up as to how it should interact with
        /// the overlay network.
        /// 
        /// What we do here is look up the port ID by MAC address, and then unblock the port. This
        /// effectively allows traffic on that port to completely bypass the VFP extension and head
        /// straight to the external network. This allows it to interact with the external network
        /// just as if it was a VM on a normal Hyper-V switch with no VFP/overlay networking enabled.
        /// </remarks>
        /// <param name="macAddress">Needs to be a MAC address of the format 00:00:00:00:00:00.</param>
        private async Task UnblockVfpPortByMacAddress(string macAddress)
        {
            // Use vfpctrl to list all of the ports.
            _logger.LogInformation($"vfpctrl: Listing ports...");
            var listProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "vfpctrl.exe"),
                ArgumentList =
                {
                    "/list-vmswitch-port",
                },
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (listProcess == null)
            {
                throw new InvalidOperationException("Failed to list VFP switch ports with vfpctrl!");
            }
            var listOutput = await listProcess.StandardOutput.ReadToEndAsync();

            var portNameRegex = new Regex("^Port name\\s+: ([A-Fa-f0-9-]+)$");
            var macAddressRegex = new Regex("^MAC address\\s+: ([A-Fa-f0-9-]+)$");

            var currentPortName = string.Empty;
            foreach (var line in listOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split(new[] { '\n' }))
            {
                var portNameMatch = portNameRegex.Match(line.Trim());
                if (portNameMatch.Success)
                {
                    currentPortName = portNameMatch.Groups[1].Value;
                    _logger.LogInformation($"vfpctrl list: Detected port name:   {currentPortName}");
                    continue;
                }
                var macAddressMatch = macAddressRegex.Match(line.Trim());
                if (macAddressMatch.Success)
                {
                    _logger.LogInformation($"vfpctrl list: Detected MAC address: {macAddressMatch.Groups[1].Value}");
                    if (macAddressMatch.Groups[1].Value.Equals(macAddress.Replace(":", "-", StringComparison.Ordinal), StringComparison.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(currentPortName))
                        {
                            throw new InvalidOperationException("MAC address is unassociated with VFP port?");
                        }
                        break;
                    }
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    // An empty line separates ports, so always clear out the port name so
                    // we can't get mismatched.
                    _logger.LogInformation($"vfpctrl list: Detected port separator (blank line)");
                    currentPortName = string.Empty;
                }
            }
            if (string.IsNullOrWhiteSpace(currentPortName))
            {
                throw new InvalidOperationException($"Unable to find VFP port for MAC address {macAddress}");
            }

            // Now that we have the port name, go and unblock the port.
            _logger.LogInformation($"vfpctrl: Attempting to unblock port: {currentPortName}");
            var unblockProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "vfpctrl.exe"),
                ArgumentList =
                {
                    "/port",
                    currentPortName,
                    "/disable-port"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (unblockProcess == null)
            {
                throw new InvalidOperationException("Failed to unblock VFP switch port with vfpctrl!");
            }
            await unblockProcess.WaitForExitAsync();
            if (unblockProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"vfpctrl exited with exit code {unblockProcess.ExitCode} during unblock operation!");
            }

            _logger.LogInformation($"vfpctrl: Successfully blocking port: {currentPortName}");
        }

        public async Task<bool> ConfigureForKubernetesAsync(bool isController, CancellationToken cancellationToken)
        {
            // Determine if the DWORD 'HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\hns\State\FwPerfImprovementChange'
            // is set to 0. If it is not, we must set it and then restart the Host Network Service.
            // This is the workaround for this bug here: https://github.com/microsoft/Windows-Containers/issues/516#issuecomment-2321754737
            _logger.LogInformation("Checking if Host Network Service requires container networking fix...");
            var registryStack = RegistryStack.OpenPath(@"HKLM:\SYSTEM\CurrentControlSet\Services\hns\State", writable: true, create: true);
            if (!registryStack.Key.GetValueNames().Contains("FwPerfImprovementChange") ||
                registryStack.Key.GetValueKind("FwPerfImprovementChange") != Microsoft.Win32.RegistryValueKind.DWord ||
                (int)registryStack.Key.GetValue("FwPerfImprovementChange")! != 0)
            {
                if (await _serviceControl.IsServiceRunning("hns", cancellationToken))
                {
                    _logger.LogInformation("Stopping Host Network Service on Windows before applying container networking fix...");
                    await _serviceControl.StopService("hns", cancellationToken);
                }

                _logger.LogInformation("Setting FwPerfImprovementChange to 0 to fix container networking on Windows...");
                registryStack.Key.SetValue("FwPerfImprovementChange", 0, Microsoft.Win32.RegistryValueKind.DWord);

                _logger.LogInformation("Starting Host Network Service on Windows now that fix has been applied.");
                await _serviceControl.StartService("hns", cancellationToken);
            }

            // Find the network adapter associated with the IP address.
            var networkAdapter = _localEthernetInfo.NetworkAdapter;
            if (networkAdapter == null)
            {
                _logger.LogCritical($"Unable to locate network adapter name for IP address {_localEthernetInfo.IPAddress}, which is necessary to set up networking on Windows.");
                Environment.ExitCode = 1;
                _hostApplicationLifetime.StopApplication();
                return false;
            }

            // Open required firewall ports.
            _logger.LogInformation("Opening TCP 10250 for kubectl exec...");
            _windowsFirewall.UpsertPortRule(
                "KubeletAllow10250",
                true,
                10250,
                Protocol.Tcp);
            _logger.LogInformation($"Opening UDP {_clusterNetworkingConfiguration.VXLANPort} for calico overlay traffic...");
            _windowsFirewall.UpsertPortRule(
                "KubernetesOverlayTrafficUDP",
                true,
                _clusterNetworkingConfiguration.VXLANPort,
                Protocol.Udp);
            _logger.LogInformation($"Opening network for kube-proxy...");
            _windowsFirewall.UpsertApplicationRule(
                "KubernetesProxy",
                true,
                Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubernetes", "node", "bin", "kube-proxy.exe"));
            if (isController)
            {
                _logger.LogInformation($"Opening UDP/TCP 53 for CoreDNS traffic...");
                _windowsFirewall.UpsertPortRule(
                    "CoreDNSTCP",
                    true,
                    53,
                    Protocol.Tcp);
                _windowsFirewall.UpsertPortRule(
                    "CoreDNSUDP",
                    true,
                    53,
                    Protocol.Udp);
                _windowsFirewall.UpsertApplicationRule(
                    "CoreDNS",
                    true,
                    Path.Combine(_pathProvider.RKMRoot, "coredns", "coredns.exe"));
            }

            // CNI configuration is no longer written out here; instead it is set up as part of
            // 'startup_script.ps1' in the 'calico-windows-node-scripts' in the Helm chart.

#if FALSE
            // Create an overlay network to trigger vSwitch creation because we need
            // a vSwitch for networking to work. We only do this once because it will
            // disrupt networking.
            if (!_hnsService.GetHnsNetworks().Any(x => x.Name == "KubernetesDummy"))
            {
                // Check that the network adapter isn't *already* a virtual switch. This
                // is the case if the machine has been used with Hyper-V and a Virtual Switch
                // has already been created on this interface. We need the existing Virtual Switch
                // removed because it won't be the same type as Overlay (which is what we need
                // for Calico). Detect this scenario and provide a useful error message.
                if (networkAdapter.Description.Contains("Hyper-V Virtual Ethernet Adapter", StringComparison.Ordinal))
                {
                    _logger.LogCritical($"The network address {_localEthernetInfo.IPAddress} already belongs to a virtual switch managed by Hyper-V, but Kubernetes needs to be able to create it's own virtual switch of a different type in order for networking to work. Remove the virtual switch from Hyper-V (via the Hyper-V Manager) and then run RKM again.");
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                    return false;
                }

                _logger.LogInformation("Adding dummy HNS network to force a virtual switch to be created... (this will disrupt network connectivity for a moment)");
                _hnsService.NewHnsNetwork(new HnsNetwork
                {
                    Type = "Overlay",
                    Name = "KubernetesDummy",
                    NetworkAdapterName = networkAdapter.Name,
                    Subnets = new[]
                    {
                        new HnsSubnet
                        {
                            // @note: These don't line up with anything, it's just to
                            // get a virtual switch on the external adapter of the
                            // correct type. As far as I can tell, the KubernetesDummy
                            // network isn't used at all; the real one that gets used
                            // is called vxlan0 (in the networkName variable).
                            AddressPrefix = "192.168.255.0/30",
                            GatewayAddress = "192.168.255.1",
                            Policies = new[]
                            {
                                new HnsSubnetPolicy
                                {
                                    Type = "VSID",
                                    VSID = 9999,
                                }
                            }
                        }
                    }
                });
            }

            // The Calico node wrapper deletes all HNS networks on startup; we can do slightly better and just cleanup
            // all non-dummy networks to avoid the network interruption.
            _logger.LogInformation("Deleting all non-dummy networks on startup to ensure consistent state...");
            foreach (var network in _hnsService.GetHnsNetworks())
            {
                if (network.Name != "KubernetesDummy")
                {
                    try
                    {
                        // Technically we will delete the WSL network here if one exists, but it doesn't matter
                        // because we're going to reconfigure WSL to use our KubernetesDummy switch anyway.
                        _hnsService.DeleteHnsNetwork(network.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Unable to clean up '{network.Name}' due to an exception: {ex.Message}");
                    }
                }
            }
#endif

            // Only on Windows controllers do we run a kubelet inside WSL. On normal Windows nodes, we only run Windows containers.
            if (isController)
            {
                // We have to force WSL to use our external Hyper-V switch. Since we've just set up a "KubernetesDummy"
                // switch via the above logic, that's what it should be called.
                var wslHostname = _wslTranslation.GetTranslatedControllerHostname();
                var wslAddress = await _wslDistro.GetWslDistroIPAddress(cancellationToken);
                if (wslAddress == null || !IsInCIDR(wslAddress, _localEthernetInfo.HostSubnetCIDR!) ||
                    !File.Exists(Path.Combine(_pathProvider.RKMRoot, "wsl", "configured")))
                {
                    // Tell WSL to use bridging on our KubernetesDummy adapter, and configure it so that
                    // systemd will start at boot and try to configure the network adapter.
                    var wslConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");
                    _logger.LogInformation("Configuring WSL to attach to the externally bridged adapter...");
                    await File.WriteAllTextAsync(wslConfigPath, @"
[wsl2]
networkingMode = bridged
vmSwitch = KubernetesDummy
dhcp = true
localhostForwarding = false
", cancellationToken);
                    Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "wsl"));
                    var wslConfigurationScriptPath = Path.Combine(_pathProvider.RKMRoot, "wsl", "configure");
                    await File.WriteAllTextAsync(wslConfigurationScriptPath, $@"
#!/bin/bash

cat >/etc/wsl.conf <<EOF
[boot]
systemd = true
[network]
hostname = {wslHostname}
generateResolvConf = false
generateHosts = false
EOF
mkdir -p /usr/lib/systemd/network

cat >/usr/lib/systemd/network/kubernetes.network <<EOF
[Match]
Name=eth0
[Network]
Description=Kubernetes
DHCP=true
EOF

cat >/etc/hosts <<EOF
127.0.0.1   localhost
127.0.1.1   {Environment.MachineName.ToLowerInvariant()}-wsl
{_localEthernetInfo.IPAddress}  {Environment.MachineName.ToLowerInvariant()}
::1     ip6-localhost ip6-loopback
fe00::0 ip6-localnet
ff00::0 ip6-mcastprefix
ff02::1 ip6-allnodes
ff02::2 ip6-allrouters
EOF

rm /etc/resolv.conf
ln -s /run/systemd/resolve/resolv.conf /etc/resolv.conf
 ".Replace("\r\n", "\n", StringComparison.Ordinal), cancellationToken);
                    var distroName = await _wslDistro.GetWslDistroName(cancellationToken);
                    var configureExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/bin/bash", _wslTranslation.TranslatePath(wslConfigurationScriptPath) }, string.Empty, Encoding.UTF8, CancellationToken.None);
                    if (configureExitCode != 0)
                    {
                        _logger.LogCritical($"Failed to configure WSL to use the external switch (got exit code {configureExitCode}). See above for details.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    // Shutdown WSL so that our next command will try to run with the new networking settings.
                    _logger.LogInformation("Forcing WSL to shutdown and restart...");
                    await _wslDistro.RunWslInvocation(new[] { "--shutdown" }, string.Empty, Encoding.UTF8, CancellationToken.None);

                    // Now get the MAC address of WSL. This will also start up WSL as it's the first command to
                    // be run since shutdown.
                    //
                    // WSL won't appear as a VFP port until it's running, so we have to run at least one command
                    // before we can call UnblockVfpPortByMacAddress. Luckily we also need to get the MAC address
                    // so we can solve two problems with one call.
                    _logger.LogInformation("Obtaining the new WSL MAC address...");
                    var macAddress = await _wslDistro.GetWslDistroMACAddress(cancellationToken);
                    _logger.LogInformation($"WSL now has an MAC address of: {macAddress}");
                    if (macAddress == null)
                    {
                        _logger.LogCritical($"Failed to get the MAC address of the WSL instance.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    // Change the hostname of the WSL instance to be "<windows hostname>-wsl" so that it's unique
                    // on the local network.
                    _logger.LogInformation($"Updating the hostname of WSL to {wslHostname}...");
                    var hostnameExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/usr/bin/hostnamectl", "set-hostname", wslHostname }, string.Empty, Encoding.UTF8, CancellationToken.None);
                    if (hostnameExitCode != 0)
                    {
                        _logger.LogCritical($"Failed to call hostnamectl in WSL (got exit code {hostnameExitCode}). Without assigning WSL a new hostname, network configuration can not continue. See above for details.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    // Unblock the WSL MAC address from the VFP extension using undocumented magic.
                    await UnblockVfpPortByMacAddress(macAddress);

                    // Now request a DHCP lease inside WSL. When WSL started up and tried to configure the network
                    // initially, it would have been blocked on the VFP port, so now we have to use dhclient to
                    // get an actual address.
                    _logger.LogInformation("Requesting WSL obtain a DHCP lease from the local network...");
                    var dhclientExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/usr/sbin/dhclient" }, string.Empty, Encoding.UTF8, CancellationToken.None);
                    if (dhclientExitCode != 0)
                    {
                        _logger.LogCritical($"Failed to call dhclient in WSL (got exit code {dhclientExitCode}). Without WSL getting a DHCP lease, network configuration can not continue. See above for details.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    // Get the newly assigned IP address from WSL.
                    _logger.LogInformation("Getting the IP address that has been assigned to WSL...");
                    wslAddress = await _wslDistro.GetWslDistroIPAddress(cancellationToken);
                    _logger.LogInformation($"WSL now has an IP address of: {wslAddress}");

                    // Check if the WSL IP address is in the subnet CIDR, just to be safe.
                    if (wslAddress == null || !IsInCIDR(wslAddress, _localEthernetInfo.HostSubnetCIDR!))
                    {
                        _logger.LogCritical($"Unable to move WSL into the local subnet CIDR {_localEthernetInfo.HostSubnetCIDR} (it was given {wslAddress}). WSL must be on the same subnet as the host in order to run a kubelet inside WSL for Linux pods.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    // We want to "lock in" the network settings so that subsequent starts of the WSL instance
                    // will not change the IP address, regardless of what the local network's DHCP wants.
                    var wslFinalizationScriptPath = Path.Combine(_pathProvider.RKMRoot, "wsl", "finalize-network");
                    await File.WriteAllTextAsync(wslFinalizationScriptPath, $@"
#!/bin/bash

set -x

# Get the resolved settings.
RETRY=0
while [[ $RETRY -lt 30 ]]; do
    IP_AND_MASK=$(ip addr show eth0 | awk '/inet /{{print $2;exit}}')
    GATEWAY=$(ip route show default | awk '//{{print $3;exit}}')
    DNS_SERVERS=$(cat /run/systemd/resolve/resolv.conf | awk '/nameserver/{{print ""DNS="" $2}}')
    if [ ""$IP_AND_MASK"" == """" ]; then
        echo ""error: unable to get IP address on WSL startup!""
        RETRY=$[$RETRY+1]
        sleep 1
        continue
    fi
    if [ ""$GATEWAY"" == """" ]; then
        echo ""error: unable to get gateway on WSL startup!""
        RETRY=$[$RETRY+1]
        sleep 1
        continue
    fi
    if [ ""$DNS_SERVERS"" == """" ]; then
        echo ""warning: unable to get DNS servers on WSL startup!""
        RETRY=$[$RETRY+1]
        sleep 1
        continue
    fi
    break
done
if [ ""$IP_AND_MASK"" == """" ]; then
    echo ""error: unable to get IP address on WSL startup!""
    exit 1
fi
if [ ""$GATEWAY"" == """" ]; then
    echo ""error: unable to get gateway on WSL startup!""
    exit 1
fi
if [ ""$DNS_SERVERS"" == """" ]; then
    echo ""warning: unable to get DNS servers on WSL startup!""
    exit 1
fi

# Fix up /etc/resolv.conf if needed.
if [ ! -e /etc/resolv.conf ]; then
    rm /etc/resolv.conf || true
    ln -s /run/systemd/resolve/resolv.conf /etc/resolv.conf
fi

# Emit the network settings.
cat >/usr/lib/systemd/network/kubernetes.network <<EOF
[Match]
Name=eth0

[Network]
Description=Kubernetes
DHCP=no
Address=$IP_AND_MASK
Gateway=$GATEWAY
$DNS_SERVERS
EOF

# Restart networkd to lock everything in place
systemctl restart systemd-networkd

exit 0
 ".Replace("\r\n", "\n", StringComparison.Ordinal), cancellationToken);
                    var finalizeExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/bin/bash", _wslTranslation.TranslatePath(wslFinalizationScriptPath), wslHostname }, string.Empty, Encoding.UTF8, CancellationToken.None);
                    if (finalizeExitCode != 0)
                    {
                        _logger.LogCritical($"Failed to finalize WSL networking details (got exit code {finalizeExitCode}). See above for details.");
                        Environment.ExitCode = 1;
                        _hostApplicationLifetime.StopApplication();
                        return false;
                    }

                    await File.WriteAllTextAsync(Path.Combine(_pathProvider.RKMRoot, "wsl", "configured"), "configured", cancellationToken);

                    // @note: We don't need to run 'sysctl net.bridge.bridge-nf-call-iptables=1' in WSL like we do
                    // for Linux networking configuration, because it seems to be enabled by default in WSL.
                }

                // Update our system-wide C:\Windows\system32\drivers\etc\hosts to include an entry for our WSL
                // machine in case our network router isn't smart enough to add it to DNS resolution. This only
                // matters for things like kubectl logs and kubectl attach which directly talk to nodes.
                _logger.LogInformation($"Adding '{wslAddress} {wslHostname}' to system-wide /etc/hosts file...");
                var hostsPath = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "drivers", "etc", "hosts");
                var hosts = await File.ReadAllLinesAsync(hostsPath, cancellationToken);
                var newHosts = hosts
                    // Remove any existing entry (in case the IP addres is rotating).
                    .Where(x => !x.Trim().EndsWith($" {wslHostname}", StringComparison.Ordinal))
                    .Where(x => !x.Trim().EndsWith($" {Environment.MachineName.ToLowerInvariant()}", StringComparison.Ordinal))
                    // Add our new entries.
                    .Concat(new[] {
                        $"{wslAddress} {wslHostname}",
                        $"127.0.0.1 {Environment.MachineName.ToLowerInvariant()}"
                    })
                    .ToArray();
                await File.WriteAllLinesAsync(hostsPath, newHosts, cancellationToken);
            }

            return true;
        }
    }
}
