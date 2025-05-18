using Redpoint.KubernetesManager.HnsApi;

namespace Redpoint.Windows.HostNetworkingService
{
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text.Json;
    using Redpoint.Windows.HostNetworkingService.ComWrapper;

    [SupportedOSPlatform("windows")]
    internal class DefaultHns : IHnsApi
    {
        IHNSApi? _hns;

        public DefaultHns()
        {
            var clsid = new Guid(ClassIds.HnsApi);
            var iid = new Guid(IHNSApi.IID);
            int hr = Ole32.CoCreateInstance(
                ref clsid,
                0,
                (uint)Ole32.CLSCTX.CLSCTX_LOCAL_SERVER,
                ref iid,
                out object comObject);
            if (hr == 0)
            {
                _hns = (IHNSApi)comObject;
            }
        }

        public HnsNetworkWithId[] GetHnsNetworks()
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var responseText = _hns.Request2("GET", "/networks", "");
            var response = JsonSerializer.Deserialize(responseText, HnsJsonSerializerContext.Default.HnsResponseHnsNetworkWithIdArray);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? [];
        }

        public void NewHnsNetwork(HnsNetwork network)
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var request = JsonSerializer.Serialize(network, HnsJsonSerializerContext.Default.HnsNetwork);
            var response = JsonSerializer.Deserialize(
                _hns.Request2("POST", "/networks", request),
                HnsJsonSerializerContext.Default.HnsResponse);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }

        public void DeleteHnsNetwork(string id)
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var response = JsonSerializer.Deserialize(
                _hns.Request2("DELETE", $"/networks/{id}", ""),
                HnsJsonSerializerContext.Default.HnsResponse);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }

        public HnsEndpointWithId[] GetHnsEndpoints()
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var responseText = _hns.Request2("GET", "/endpoints", "");
            var response = JsonSerializer.Deserialize(responseText, HnsJsonSerializerContext.Default.HnsResponseHnsEndpointWithIdArray);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? [];
        }

        public HnsPolicyListWithId[] GetHnsPolicyLists()
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var responseText = _hns.Request2("GET", "/policylists", "");
            var response = JsonSerializer.Deserialize(responseText, HnsJsonSerializerContext.Default.HnsResponseHnsPolicyListWithIdArray);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
            return response?.Output ?? [];
        }

        public void DeleteHnsPolicyList(string id)
        {
            if (_hns == null)
            {
                throw new HnsNotAvailableException();
            }
            var response = JsonSerializer.Deserialize(
                _hns.Request2("DELETE", $"/policylists/{id}", ""),
                HnsJsonSerializerContext.Default.HnsResponse);
            if (!(response?.Success ?? false))
            {
                throw new InvalidOperationException("HNS operation failed!");
            }
        }
    }
}
