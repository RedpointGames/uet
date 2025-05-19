using Redpoint.KubernetesManager.Models.Hcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager.Models
{
    [JsonSerializable(typeof(HcsComputeSystem))]
    [JsonSerializable(typeof(HcsComputeSystemWithId))]
    [JsonSerializable(typeof(HcsComputeSystemWithId[]))]
    internal partial class WindowsHostJsonSerializerContext : JsonSerializerContext
    {
    }
}
