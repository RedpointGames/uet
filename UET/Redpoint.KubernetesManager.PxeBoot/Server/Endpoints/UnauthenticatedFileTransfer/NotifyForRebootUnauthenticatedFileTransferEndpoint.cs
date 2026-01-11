namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class NotifyForRebootUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        private readonly ILogger<NotifyForRebootUnauthenticatedFileTransferEndpoint> _logger;

        public NotifyForRebootUnauthenticatedFileTransferEndpoint(
            ILogger<NotifyForRebootUnauthenticatedFileTransferEndpoint> logger)
        {
            _logger = logger;
        }

        public string[] Prefixes => ["/notify-for-reboot"];

        public async Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (request.PathRemaining.HasValue ||
                request.IsTftp ||
                request.HttpContext == null)
            {
                // Only matches "/notify-for-reboot" exactly.
                return null;
            }

            string aikFingerprint;
            if (!request.HttpContext.Request.Query.TryGetValue("fingerprint", out var fingerprints) ||
                fingerprints.Count != 1 ||
                string.IsNullOrWhiteSpace(fingerprints[0]))
            {
                // 'fingerprint' query string isn't valid.
                return null;
            }
            aikFingerprint = fingerprints[0]!;

            var node = await request.ConfigurationSource.GetRkmNodeByAttestationIdentityKeyFingerprintAsync(
                aikFingerprint,
                CancellationToken.None);

            if (node?.Status?.Provisioner != null)
            {
                _logger.LogInformation("Node has notified that it has completed a once-only reboot.");

                await request.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    node.Status.AttestationIdentityKeyFingerprint!,
                    $"Node has notified that it has completed a once-only reboot",
                    cancellationToken);

                node.Status.Provisioner.RebootNotificationForOnceViaNotifyOccurred = true;
                await request.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    node.Status.AttestationIdentityKeyFingerprint!,
                    node.Status,
                    CancellationToken.None);
            }

            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                writer.Write("ok");
            }
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}
