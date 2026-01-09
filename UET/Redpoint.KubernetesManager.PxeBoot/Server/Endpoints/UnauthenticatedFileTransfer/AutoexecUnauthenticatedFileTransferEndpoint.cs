namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class AutoexecUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        private readonly ILogger<AutoexecUnauthenticatedFileTransferEndpoint> _logger;
        private readonly IProvisionerHasher _provisionerHasher;
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;

        public AutoexecUnauthenticatedFileTransferEndpoint(
            ILogger<AutoexecUnauthenticatedFileTransferEndpoint> logger,
            IEnumerable<IProvisioningStep> provisioningSteps,
            IProvisionerHasher provisionerHasher)
        {
            _logger = logger;
            _provisionerHasher = provisionerHasher;
            _provisioningSteps = provisioningSteps.ToDictionary(
                k => k.Type,
                v => v,
                StringComparer.OrdinalIgnoreCase);
        }

        public string[] Prefixes => ["/autoexec.ipxe", "/autoexec-nodhcp.ipxe"];

        public async Task<Stream?> GetDownloadStreamAsync(UnauthenticatedFileTransferRequest request, CancellationToken cancellationToken)
        {
            if (request.IsTftp)
            {
                if (request.PathPrefix == "/autoexec.ipxe")
                {
                    // Chain the client into HTTP so that we have faster file transfers.
                    var stream = new MemoryStream();
                    using (var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
                    {
                        writer.Write(
                            $$"""
                            #!ipxe
                            dhcp
                            chain --replace http://${next-server}:{{request.HostHttpPort}}/autoexec-nodhcp.ipxe
                            """);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    return stream;
                }
                else
                {
                    // autoexec-nodhcp.ipxe should not be accessed over TFTP.
                    return null;
                }
            }

            // Return the autoexec.ipxe script.
            string script;
            if (request.PathPrefix == "/autoexec.ipxe")
            {
                script = await GetAutoexecScript(
                    request,
                    false,
                    cancellationToken);
            }
            else
            {
                script = await GetAutoexecScript(
                    request,
                    true,
                    cancellationToken);
            }
            {
                var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
                {
                    writer.Write(script);
                }
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }
        }

        private async Task<string> GetAutoexecScript(
            UnauthenticatedFileTransferRequest request,
            bool skipDhcp,
            CancellationToken cancellationToken)
        {
            var defaultScript =
                $$$"""
                #!ipxe
                [[step:dhcp]]
                kernel static/vmlinuz rkm-api-address=[[provision:apiAddressIp]] rkm-booted-from-step-index=[[provision:bootedFromStepIndex]]
                initrd static/initrd
                initrd static/uet     /usr/bin/uet-bootstrap  mode=555
                boot
                """;

            var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                request.RemoteAddress.ToString(),
                cancellationToken);

            async Task<string> GetSelectedScript()
            {
                if (node == null)
                {
                    return defaultScript;
                }
                if (request.HttpContext == null)
                {
                    // TFTP should not be delivering an autoexec.ipxe file that does anything other than chain.
                    throw new InvalidOperationException("TFTP attempted to deliver non-chain autoexec.ipxe file!");
                }
                if (string.IsNullOrWhiteSpace(node?.Status?.Provisioner?.Name) ||
                    string.IsNullOrWhiteSpace(node?.Spec?.NodeGroup))
                {
                    _logger.LogInformation("Returning default initrd script as node is not currently provisioning or does not have a node group set.");
                    return defaultScript;
                }
                var group = await request.ConfigurationSource.GetRkmNodeGroupAsync(
                    node.Spec.NodeGroup,
                    cancellationToken);
                if (group == null)
                {
                    _logger.LogInformation("Returning default initrd script as node's group could not be found.");
                    return defaultScript;
                }
                var provisioner = await request.ConfigurationSource.GetRkmNodeProvisionerAsync(
                    node.Status.Provisioner.Name,
                    request.JsonSerializerContext.RkmNodeProvisioner,
                    cancellationToken);
                if (provisioner == null)
                {
                    _logger.LogInformation("Returning default initrd script as node's provisioner could not be found.");
                    return defaultScript;
                }
                var groupProvisioner = provisioner;
                if (group.Spec?.Provisioner != null &&
                    node.Status.Provisioner.Name != group.Spec.Provisioner)
                {
                    groupProvisioner = await request.ConfigurationSource.GetRkmNodeProvisionerAsync(
                        group.Spec.Provisioner,
                        request.JsonSerializerContext.RkmNodeProvisioner,
                        cancellationToken);
                }
                var provisionerHash = _provisionerHasher.GetProvisionerHash(
                    new ServerSideVariableContext
                    {
                        RkmNode = node,
                        RkmNodeGroup = group,
                        RkmNodeProvisioner = groupProvisioner ?? provisioner,
                        ApiHostAddress = request.HttpContext!.Connection.LocalIpAddress.ToString(),
                        ApiHostHttpPort = request.HostHttpPort,
                        ApiHostHttpsPort = request.HostHttpsPort,
                    });
                if (!string.IsNullOrWhiteSpace(node.Status.Provisioner.Hash) &&
                    provisionerHash != node.Status.Provisioner.Hash)
                {
                    _logger.LogInformation($"Returning default initrd script as node's cached provisioner hash of {node.Status.Provisioner.Hash} does not match actual hash {provisionerHash}.");
                    return defaultScript;
                }
                if (!node.Status.Provisioner.RebootStepIndex.HasValue)
                {
                    _logger.LogInformation($"Returning default initrd script as node has not yet hit a reboot step index.");
                    return defaultScript;
                }
                var provisionerStepCount = provisioner.Spec?.Steps?.Count ?? 0;
                var rebootStepIndex = node.Status.Provisioner.RebootStepIndex ?? 0;
                if (provisionerStepCount <= rebootStepIndex)
                {
                    _logger.LogInformation("Returning default initrd script as node's reboot step index exceeds provision step count.");
                    return defaultScript;
                }

                var serverContext = new IpxeProvisioningStepServerContext(request.RemoteAddress);

                var rebootStep = provisioner.Spec!.Steps![rebootStepIndex];
                var provisioningRebootStep = _provisioningSteps[rebootStep!.Type];

                var overrideScript = await provisioningRebootStep.GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
                    rebootStep!.DynamicSettings,
                    node.Status,
                    serverContext,
                    cancellationToken);
                if (overrideScript != null)
                {
                    if (string.IsNullOrWhiteSpace(overrideScript) || !overrideScript.StartsWith("#!ipxe", StringComparison.Ordinal))
                    {
                        _logger.LogWarning("Reboot script is not valid, ignoring!");
                        return
                            $$$"""
                            #!ipxe
                            [[step:dhcp]]
                            echo
                            echo REBOOT SCRIPT IS INVALID, PLEASE FIX YOUR PROVISIONER STEPS
                            echo
                            echo (waiting 30 seconds, then continuing with default initrd environment)
                            echo
                            sleep 30
                            kernel static/vmlinuz rkm-api-address=[[provision:apiAddressIp]] rkm-booted-from-step-index=-1
                            initrd static/initrd
                            initrd static/uet     /usr/bin/uet-bootstrap  mode=555
                            boot
                            """;
                    }
                }

                if (provisioningRebootStep.Flags.HasFlag(ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched) &&
                    rebootStepIndex == node.Status.Provisioner.CurrentStepIndex)
                {
                    _logger.LogInformation($"Provisioning: '{node.Status.AttestationIdentityKeyFingerprint?.Substring(0, 8)}' is completing step '{rebootStep!.Type}' at index {rebootStepIndex}.");

                    await provisioningRebootStep.ExecuteOnServerUncastedAfterAsync(
                        rebootStep!.DynamicSettings,
                        node.Status,
                        serverContext,
                        cancellationToken);

                    // Increment current step index and then update node status.
                    if (!node.Status.Provisioner.CurrentStepIndex.HasValue)
                    {
                        node.Status.Provisioner.CurrentStepIndex = 0;
                    }
                    node.Status.Provisioner.CurrentStepIndex += 1;
                    if (node.Status.Provisioner.CurrentStepIndex <= node.Status.Provisioner.RebootStepIndex)
                    {
                        // Make sure when the client grabs the next step, it's always continuing from the reboot point
                        // if a later step hasn't committed.
                        node.Status.Provisioner.CurrentStepIndex = node.Status.Provisioner.RebootStepIndex + 1;
                    }
                    if (node.Status.Provisioner.CurrentStepIndex >= provisioner.Spec.Steps.Count)
                    {
                        node.Status.Provisioner = null;
                    }
                    else
                    {
                        node.Status.Provisioner.CurrentStepStarted = false;
                    }

                    // We never automatically start the next step in this context, because we aren't returning
                    // the next step data to UET; instead we're returning the IPXE script used for booting.
                    await request.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                        node.Status.AttestationIdentityKeyFingerprint!,
                        node.Status,
                        cancellationToken);
                }

                return overrideScript ?? defaultScript;
            }

            var bootedFromStepIndex = (node?.Status?.Provisioner?.RebootStepIndex ?? -1).ToString(CultureInfo.InvariantCulture);
            _logger.LogInformation($"Informing machine that they are booting from step index {bootedFromStepIndex}.");

            // This is a very limited subset of the substitutions done by the variable provider.
            var replacements = new Dictionary<string, string>
            {
                { "provision:bootedFromStepIndex", bootedFromStepIndex },
                { "provision:apiAddressIp", request.HttpContext!.Connection.LocalIpAddress.ToString() },
                { "step:dhcp", !skipDhcp ? "dhcp" : string.Empty },
            };
            var selectedScript = await GetSelectedScript();
            foreach (var kv in replacements)
            {
                selectedScript = selectedScript.Replace("[[" + kv.Key + "]]", kv.Value, StringComparison.Ordinal);
            }
            return selectedScript;
        }
    }
}
