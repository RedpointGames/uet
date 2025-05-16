namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager;
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;

    internal class DefaultLocalEthernetInfo : ILocalEthernetInfo
    {
        private readonly ILogger<DefaultLocalEthernetInfo> _logger;
        private Lazy<IPAddress?> _ipAddress;

        public DefaultLocalEthernetInfo(ILogger<DefaultLocalEthernetInfo> logger)
        {
            _logger = logger;
            _ipAddress = new Lazy<IPAddress?>(GetExternalIPAddress);
        }

        public bool HasIPAddress => _ipAddress.Value != null;

        public IPAddress IPAddress => _ipAddress.Value!;

        private IPAddress? GetExternalIPAddress()
        {
            var myIPHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var myIPAddress in myIPHostEntry.AddressList)
            {
                if (myIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    if (!IsPrivateIP(myIPAddress))
                    {
                        _logger.LogInformation($"Detected local IP address as: {myIPAddress.ToString()}");
                        return myIPAddress;
                    }
                }
            }

            // On Windows when we start as a service, it might be too early to resolve our computer
            // name via DNS. Therefore, keep trying up to 10 minutes after the machine boot to get
            // the IP address.
            if (OperatingSystem.IsWindows())
            {
                var timeRemaining = 10 - new TimeSpan(10000 * WindowsUtilities.GetTickCount64()).TotalMinutes;
                if (timeRemaining > 0)
                {
                    _logger.LogWarning($"RKM started too early to resolve the computer's name via DNS. Will retry for another {timeRemaining} minutes to resolve it...");
                    do
                    {
                        // This isn't an async function, so just thread sleep. This code
                        // will run early enough in the startup sequence that this should
                        // not be a problem.
                        Thread.Sleep(15000);

                        // Try resolving again.
                        myIPHostEntry = Dns.GetHostEntry(Dns.GetHostName());
                        foreach (var myIPAddress in myIPHostEntry.AddressList)
                        {
                            if (myIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                if (!IsPrivateIP(myIPAddress))
                                {
                                    _logger.LogInformation($"Detected local IP address as: {myIPAddress.ToString()}");
                                    return myIPAddress;
                                }
                            }
                        }

                        timeRemaining = 10 - new TimeSpan(10000 * WindowsUtilities.GetTickCount64()).TotalMinutes;
                        _logger.LogWarning($"Still unable to resolve computer name to IP address via DNS, will retry for another {timeRemaining} minutes to resolve it...");
                    }
                    while (timeRemaining > 0);
                }
            }

            _logger.LogError($"Unable to detect local IP address of machine!");
            return null;
        }

        public bool IsLoopbackAddress(IPAddress address)
        {
            return IsPrivateIP(address);
        }

        private static bool IsPrivateIP(IPAddress myIPAddress)
        {
            if (myIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var ipBytes = myIPAddress.GetAddressBytes();

                // 127.0.0.0/24 
                if (ipBytes[0] == 127)
                {
                    return true;
                }
                // 172.0.0.0/24 
                else if (ipBytes[0] == 172)
                {
                    return true;
                }
                // 169.254.0.0/16
                else if (ipBytes[0] == 169 && ipBytes[1] == 254)
                {
                    return true;
                }
            }

            return false;
        }

        public NetworkInterface? NetworkAdapter
        {
            get
            {
                var networkAdapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(x => x.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(IPAddress)));

                if (networkAdapter == null && OperatingSystem.IsWindows())
                {
                    var timeRemaining = 10 - new TimeSpan(10000 * WindowsUtilities.GetTickCount64()).TotalMinutes;
                    if (timeRemaining > 0)
                    {
                        _logger.LogWarning($"RKM started too early to determine the network adapter for the local IP address. Will retry for another {timeRemaining} minutes to resolve it...");
                        do
                        {
                            // This isn't an async function, so just thread sleep. This code
                            // will run early enough in the startup sequence that this should
                            // not be a problem.
                            Thread.Sleep(15000);

                            // Try resolving again.
                            networkAdapter = NetworkInterface.GetAllNetworkInterfaces()
                                .FirstOrDefault(x => x.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(IPAddress)));
                            if (networkAdapter != null)
                            {
                                return networkAdapter;
                            }

                            timeRemaining = 10 - new TimeSpan(10000 * WindowsUtilities.GetTickCount64()).TotalMinutes;
                            _logger.LogWarning($"Still unable to determine the network adapter for the local IP address, will retry for another {timeRemaining} minutes to resolve it...");
                        }
                        while (timeRemaining > 0);
                    }
                }

                return networkAdapter;
            }
        }

        public string? HostSubnetCIDR
        {
            get
            {
                var networkAdapter = NetworkAdapter;
                if (networkAdapter == null)
                {
                    return null;
                }
                var associatedIpAddress = networkAdapter.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.Equals(IPAddress))!;
                var maskedIpAddressBits = BitConverter.ToUInt32(associatedIpAddress.Address.GetAddressBytes());
                for (var i = 0; i < associatedIpAddress.PrefixLength; i++)
                {
                    maskedIpAddressBits &= ~(0x1u << 31 - i);
                }
                var maskedIpAddress = new IPAddress(BitConverter.GetBytes(maskedIpAddressBits));
                var subnetCIDR = $"{maskedIpAddress}/{associatedIpAddress.PrefixLength}";
                return subnetCIDR;
            }
        }
    }
}
