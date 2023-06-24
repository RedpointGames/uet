namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    [JsonSerializable(typeof(KubernetesDockerConfig))]
    internal partial class KubernetesJsonSerializerContext : JsonSerializerContext
    {
    }
}
