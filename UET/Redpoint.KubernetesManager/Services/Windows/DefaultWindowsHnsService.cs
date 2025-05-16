namespace Redpoint.KubernetesManager.Services.Windows
{
    using HNSApiLib;
    using Redpoint.KubernetesManager.Models.Hns;
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text.Json;

    [SupportedOSPlatform("windows")]
    internal class DefaultWindowsHnsService : IWindowsHnsService
    {
        IHNSApi? _hns;

        public DefaultWindowsHnsService()
        {
            try
            {
                _hns = new HNSApiClass();
            }
            catch (COMException ex) when (ex.Message.Contains("REGDB_E_CLASSNOTREG"))
            {
                // Expected if Windows Containers isn't installed yet.
                _hns = null;
            }
        }

        public HnsNetworkWithId[] GetHnsNetworks()
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var responseText = _hns.Request2("GET", "/networks", "");
            var response = JsonSerializer.Deserialize<HnsResponse<HnsNetworkWithId[]?>>(responseText);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? new HnsNetworkWithId[0];
        }

        public void NewHnsNetwork(HnsNetwork network)
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var request = JsonSerializer.Serialize(network, network.GetType());
            var response = JsonSerializer.Deserialize<HnsResponse>(
                _hns.Request2("POST", "/networks", request));
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }

        public void DeleteHnsNetwork(string id)
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var response = JsonSerializer.Deserialize<HnsResponse>(
                _hns.Request2("DELETE", $"/networks/{id}", ""));
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }

        public HnsEndpointWithId[] GetHnsEndpoints()
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var responseText = _hns.Request2("GET", "/endpoints", "");
            var response = JsonSerializer.Deserialize<HnsResponse<HnsEndpointWithId[]?>>(responseText);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? new HnsEndpointWithId[0];
        }

        public HnsPolicyListWithId[] GetHnsPolicyLists()
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var responseText = _hns.Request2("GET", "/policylists", "");
            var response = JsonSerializer.Deserialize<HnsResponse<HnsPolicyListWithId[]?>>(responseText);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? new HnsPolicyListWithId[0];
        }

        public void DeleteHnsPolicyList(string id)
        {
            if (_hns == null)
            {
                throw new InvalidOperationException("HNS was not available when rkm was started, most likely because Windows Containers wasn't installed at the time. Allow the machine to restart to set this node up.");
            }
            var response = JsonSerializer.Deserialize<HnsResponse>(
                _hns.Request2("DELETE", $"/policylists/{id}", ""));
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }
    }
}
