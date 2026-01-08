namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Redpoint.Collections;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class SyncBootEntriesProvisioningEndpoint : INodeProvisioningEndpoint
    {
        public string Path => "/sync-boot-entries";

        public bool RequireNodeObjects => true;

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            var entries = (await JsonSerializer.DeserializeAsync(
                context.Request.Body,
                context.JsonSerializerContext.ListRkmNodeStatusBootEntry,
                context.CancellationToken))!;

            context.RkmNode!.Status ??= new();
            context.RkmNode!.Status.BootEntries = entries;

            await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                context.AikFingerprint,
                context.RkmNode.Status,
                context.CancellationToken);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                (context.RkmNode!.Spec?.InactiveBootEntries ?? []).WhereNotNull().ToList(),
                context.JsonSerializerContext.IListString,
                context.CancellationToken);
        }
    }
}
