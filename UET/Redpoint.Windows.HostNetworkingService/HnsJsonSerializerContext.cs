using Redpoint.Windows.HostNetworkingService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager.HnsApi
{
    [JsonSerializable(typeof(HnsEndpoint))]
    [JsonSerializable(typeof(HnsEndpointWithId))]
    [JsonSerializable(typeof(HnsNetwork))]
    [JsonSerializable(typeof(HnsNetworkWithId))]
    [JsonSerializable(typeof(HnsPolicyList))]
    [JsonSerializable(typeof(HnsPolicyListWithId))]
    [JsonSerializable(typeof(HnsResponse))]
    [JsonSerializable(typeof(HnsResponse<HnsNetworkWithId[]?>))]
    [JsonSerializable(typeof(HnsResponse<HnsEndpointWithId[]?>))]
    [JsonSerializable(typeof(HnsResponse<HnsPolicyListWithId[]?>))]
    [JsonSerializable(typeof(HnsSubnet))]
    [JsonSerializable(typeof(HnsSubnetPolicy))]
    public partial class HnsJsonSerializerContext : JsonSerializerContext
    {
    }
}
