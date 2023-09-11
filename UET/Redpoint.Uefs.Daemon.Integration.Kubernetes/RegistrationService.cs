namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Pluginregistration;
    using System.Threading.Tasks;

    internal sealed class RegistrationService : Registration.RegistrationBase
    {
        private readonly ILogger _logger;

        public RegistrationService(ILogger<RegistrationService> logger)
        {
            _logger = logger;
        }

        public override Task<PluginInfo> GetInfo(InfoRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Kubelet requested information about our CSI plugin.");

            return Task.FromResult(new PluginInfo
            {
                Type = "CSIPlugin",
                Name = "uefs.redpoint.games",
                SupportedVersions = { "1.0.0" },
            });
        }

        public override Task<RegistrationStatusResponse> NotifyRegistrationStatus(RegistrationStatus request, ServerCallContext context)
        {
            if (!request.PluginRegistered)
            {
                _logger.LogError($"CSI plugin is not registered with Kubernetes: {request.Error}");
            }

            return Task.FromResult(new RegistrationStatusResponse());
        }
    }
}
