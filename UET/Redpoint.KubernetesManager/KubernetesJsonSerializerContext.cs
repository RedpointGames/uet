using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager
{
    using Redpoint.KubernetesManager.Models;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(LegacyNodeManifest))]
    internal partial class KubernetesJsonSerializerContext : JsonSerializerContext
    {
    }
}
