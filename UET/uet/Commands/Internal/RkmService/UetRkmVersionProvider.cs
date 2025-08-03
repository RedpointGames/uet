using Redpoint.KubernetesManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UET.Services;

namespace UET.Commands.Internal.RkmService
{
    class UetRkmVersionProvider : IRkmVersionProvider
    {
        private readonly ISelfLocation _selfLocation;

        public UetRkmVersionProvider(
            ISelfLocation selfLocation)
        {
            _selfLocation = selfLocation;
        }

        private static Lazy<string> _version = new Lazy<string>(() => RedpointSelfVersion.GetInformationalVersion() ?? "dev");

        public string Version => _version.Value;

        public string UetFilePath => _selfLocation.GetUetLocalLocation(true);
    }
}
