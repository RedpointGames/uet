using Redpoint.KubernetesManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UET.Commands.Internal.RkmService
{
    class UetRkmVersionProvider : IRkmVersionProvider
    {
        private static Lazy<string> _version = new Lazy<string>(() => RedpointSelfVersion.GetInformationalVersion() ?? "dev");

        public string Version => _version.Value;
    }
}
