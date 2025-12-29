namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System.Net;
    using System.Text.Json.Serialization;

    internal interface INodeProvisioningEndpointContext
    {
        HttpContext HttpContext { get; }

        HttpRequest Request => HttpContext.Request;

        HttpResponse Response => HttpContext.Response;

        CancellationToken CancellationToken => HttpContext.RequestAborted;

        string AikPem { get; }

        string AikFingerprint { get; }

        string AikFingerprintShort => AikFingerprint.Substring(0, 8);

        IRkmConfigurationSource ConfigurationSource { get; }

        RkmNode? RkmNode { get; }

        RkmNodeGroup? RkmNodeGroup { get; }

        RkmNodeProvisioner? RkmNodeGroupProvisioner { get; }

        RkmNodeProvisioner? RkmNodeProvisioner { get; set; }

        KubernetesRkmJsonSerializerContext JsonSerializerContext { get; }

        DirectoryInfo NodeFileStorageDirectory { get; }

        string HostAddress { get; }

        int HostHttpPort { get; }

        int HostHttpsPort { get; }

        void UpdateRegisteredIpAddressesForNode();

        void MarkProvisioningCompleteForNode();
    }
}
