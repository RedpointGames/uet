using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager
{
    using YamlDotNet.Serialization;

    [YamlStaticContext]
    public partial class KubernetesYamlStaticContext : YamlDotNet.Serialization.StaticContext
    {
    }
}
