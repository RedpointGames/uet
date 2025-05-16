using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.KubernetesManager
{
    public interface IRkmVersionProvider
    {
        string Version { get; }
    }
}
