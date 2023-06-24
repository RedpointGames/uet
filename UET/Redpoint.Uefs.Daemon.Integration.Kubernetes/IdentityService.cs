namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using Csi.V1;
    using Grpc.Core;
    using System.Threading.Tasks;

    internal class IdentityService : Identity.IdentityBase
    {
        public override Task<ProbeResponse> Probe(ProbeRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ProbeResponse
            {
                // @todo: We can probably improve this.
                Ready = true,
            });
        }

        public override Task<GetPluginInfoResponse> GetPluginInfo(GetPluginInfoRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GetPluginInfoResponse
            {
                Name = "uefs.redpoint.games",
                VendorVersion = "dev",
                // @todo: Do we need to set the manifest field?
            });
        }

        public override Task<GetPluginCapabilitiesResponse> GetPluginCapabilities(GetPluginCapabilitiesRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GetPluginCapabilitiesResponse
            {
                // We don't have any additional capabilities.
            });
        }
    }
}
