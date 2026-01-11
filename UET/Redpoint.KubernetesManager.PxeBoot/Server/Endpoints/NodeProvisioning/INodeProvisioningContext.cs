namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System.Net;

    internal interface INodeProvisioningContext
    {
        RkmNode? RkmNode { get; }

        RkmNodeGroup? RkmNodeGroup { get; }

        RkmNodeProvisioner? RkmNodeGroupProvisioner { get; }

        RkmNodeProvisioner? RkmNodeProvisioner { get; set; }

        string HostAddress { get; }

        int HostHttpPort { get; }

        int HostHttpsPort { get; }

        IRkmConfigurationSource ConfigurationSource { get; }

        CancellationToken CancellationToken { get; }

        string AikFingerprint { get; }

        IPAddress RemoteIpAddress { get; }
    }
}
