namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using System.Net;
    using System.Text.Json.Serialization;

    internal interface INodeProvisioningEndpointContext : INodeProvisioningContext
    {
        HttpContext HttpContext { get; }

        HttpRequest Request => HttpContext.Request;

        HttpResponse Response => HttpContext.Response;

        string AikPem { get; }

        string AikFingerprintShort => AikFingerprint.Substring(0, 8);

        KubernetesRkmJsonSerializerContext JsonSerializerContext { get; }

        DirectoryInfo NodeFileStorageDirectory { get; }
    }
}
