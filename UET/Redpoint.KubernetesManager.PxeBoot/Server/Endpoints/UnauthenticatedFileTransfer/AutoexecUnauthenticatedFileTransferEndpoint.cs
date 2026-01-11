namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
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
    using System.Threading;
    using System.Threading.Tasks;

    internal class AutoexecUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        private readonly ILogger<AutoexecUnauthenticatedFileTransferEndpoint> _logger;
        private readonly IProvisionerHasher _provisionerHasher;
        private readonly IRelatedObjectLoader _relatedObjectLoader;
        private readonly IProvisioningStateManager _provisioningStateManager;
        private readonly Dictionary<string, IProvisioningStep> _provisioningSteps;

        public AutoexecUnauthenticatedFileTransferEndpoint(
            ILogger<AutoexecUnauthenticatedFileTransferEndpoint> logger,
            IEnumerable<IProvisioningStep> provisioningSteps,
            IProvisionerHasher provisionerHasher,
            IRelatedObjectLoader relatedObjectLoader,
            IProvisioningStateManager provisioningStateManager)
        {
            _logger = logger;
            _provisionerHasher = provisionerHasher;
            _relatedObjectLoader = relatedObjectLoader;
            _provisioningStateManager = provisioningStateManager;
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
                    // Try to find a node associated with the IP address in case we can use a faster DHCP command.
                    var dhcpCommand = "ifconf -c dhcp";
                    var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                        request.RemoteAddress.ToString(),
                        cancellationToken);
                    if (!string.IsNullOrWhiteSpace(node?.Spec?.BootFromNetworkAdapter))
                    {
                        dhcpCommand = $"ifconf -c dhcp {node?.Spec?.BootFromNetworkAdapter}";
                    }

                    // Chain the client into HTTP so that we have faster file transfers.
                    var stream = new MemoryStream();
                    using (var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
                    {
                        writer.Write(
                            $$"""
                            #!ipxe
                            {{dhcpCommand}}
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

        private record class AutoexecScriptTemplate
        {
            public required string ScriptTemplate { get; set; }

            public required string BootEventMessage { get; set; }
        }

        private class AutoexecNodeProvisioningContext : INodeProvisioningContext
        {
            public AutoexecNodeProvisioningContext(
                RkmNode rkmNode,
                RkmNodeGroup? rkmNodeGroup,
                RkmNodeProvisioner? rkmNodeGroupProvisioner,
                RkmNodeProvisioner? rkmNodeProvisioner,
                string hostAddress,
                int hostHttpPort,
                int hostHttpsPort,
                IRkmConfigurationSource configurationSource,
                IPAddress remoteIpAddress,
                CancellationToken cancellationToken)
            {
                RkmNode = rkmNode;
                RkmNodeGroup = rkmNodeGroup;
                RkmNodeGroupProvisioner = rkmNodeGroupProvisioner;
                RkmNodeProvisioner = rkmNodeProvisioner;
                HostAddress = hostAddress;
                HostHttpPort = hostHttpPort;
                HostHttpsPort = hostHttpsPort;
                ConfigurationSource = configurationSource;
                CancellationToken = cancellationToken;
                RemoteIpAddress = remoteIpAddress;
            }

            public RkmNode? RkmNode { get; }

            public RkmNodeGroup? RkmNodeGroup { get; }

            public RkmNodeProvisioner? RkmNodeGroupProvisioner { get; }

            public RkmNodeProvisioner? RkmNodeProvisioner { get; set; }

            public string HostAddress { get; }

            public int HostHttpPort { get; }

            public int HostHttpsPort { get; }

            public IRkmConfigurationSource ConfigurationSource { get; }

            public CancellationToken CancellationToken { get; }

            public string AikFingerprint => RkmNode?.Status?.AttestationIdentityKeyFingerprint!;

            public IPAddress RemoteIpAddress { get; }
        }

        private async Task<AutoexecScriptTemplate> GetAutoexecScriptTemplate(
            UnauthenticatedFileTransferRequest request,
            RkmNode? node,
            CancellationToken cancellationToken)
        {
            var defaultScript = new AutoexecScriptTemplate
            {
                ScriptTemplate =
                    $$$"""
                    #!ipxe
                    [[step:dhcp]]
                    kernel static/vmlinuz rkm-api-address=[[provision:apiAddressIp]] rkm-booted-from-step-index=[[provision:bootedFromStepIndex]]
                    initrd static/initrd
                    initrd static/uet     /usr/bin/uet-bootstrap  mode=555
                    boot
                    """,
                BootEventMessage = "Booting to provisioning environment",
            };

            if (node == null)
            {
                return defaultScript;
            }

            var relatedObjects = await _relatedObjectLoader.LoadRelatedObjectsAsync(
                request.ConfigurationSource,
                node,
                request.JsonSerializerContext,
                cancellationToken);

            var nodeProvisioningContext = new AutoexecNodeProvisioningContext(
                node,
                relatedObjects.RkmNodeGroup,
                relatedObjects.RkmNodeGroupProvisioner,
                relatedObjects.RkmNodeProvisioner,
                request.HttpContext!.Connection.LocalIpAddress.ToString(),
                request.HostHttpPort,
                request.HostHttpsPort,
                request.ConfigurationSource,
                request.RemoteAddress,
                cancellationToken);

            var provisioningState = await _provisioningStateManager.UpdateStateAsync(
                nodeProvisioningContext,
                true);

            switch (provisioningState)
            {
                case ProvisioningResponse.Misconfigured:
                    _logger.LogWarning("Provisioning is misconfigured for this machine. Booting to default environment.");
                    return defaultScript;
                case ProvisioningResponse.Complete:
                    {
                        if ((node.Status?.BootToDisk ?? false) &&
                            !string.IsNullOrWhiteSpace(node.Status?.BootEfiPath))
                        {
                            _logger.LogInformation($"Requesting node boot to EFI path: {node.Status.BootEfiPath}");

                            await request.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                                node.Status.AttestationIdentityKeyFingerprint!,
                                $"Booting to disk",
                                cancellationToken);

                            // @note: We no longer turn off "boot to disk" as the autoexec endpoint now uses the same
                            // logic as the "step" provisioning endpoints.
                            /*
                            node.Status.BootToDisk = false;
                            await request.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                                node.Status.AttestationIdentityKeyFingerprint!,
                                node.Status,
                                cancellationToken);
                            */

                            return new AutoexecScriptTemplate
                            {
                                ScriptTemplate =
                                    $"""
                                    #!ipxe
                                    [[step:dhcp]]

                                    sanboot --drive 0 --extra {node.Status.BootEfiPath}
                        
                                    echo
                                    echo FAILED TO BOOT TO EFI IMAGE {node.Status.BootEfiPath} ON DISK
                                    echo
                                    echo (waiting 30 seconds, then reprovisioning this machine)
                                    echo
                                    sleep 30
                        
                                    kernel static/vmlinuz rkm-api-address=[[provision:apiAddressIp]] rkm-in-recovery
                                    initrd static/initrd
                                    initrd static/uet     /usr/bin/uet-bootstrap  mode=555
                                    boot
                                    """,
                                BootEventMessage = "Booting to disk",
                            };
                        }
                        else
                        {
                            _logger.LogWarning("Provisioning is complete, but node is not marked to boot to disk. Booting to default environment.");
                            return defaultScript;
                        }
                    }
                case ProvisioningResponse.Reboot:
                    // In this case, the machine wants to reboot for force reprovisioning. We're at the boot point, so boot into the default script.
                    _logger.LogInformation("Force provisioning is requested in autoexec. Booting to default environment.");
                    return defaultScript;
            }

            if (nodeProvisioningContext.RkmNodeGroupProvisioner == null)
            {
                _logger.LogWarning("Provisioning is not complete on this machine, but the node group has no provisioner assigned. Booting to default environment.");
                return defaultScript;
            }
            if (!(node.Status?.Provisioner?.RebootStepIndex.HasValue ?? false))
            {
                _logger.LogInformation($"Returning default initrd script as node has not yet hit a reboot step index.");
                return defaultScript;
            }

            var provisionerStepCount = nodeProvisioningContext.RkmNodeGroupProvisioner.Spec?.Steps?.Count ?? 0;
            var rebootStepIndex = node.Status.Provisioner.RebootStepIndex ?? 0;
            if (provisionerStepCount <= rebootStepIndex)
            {
                _logger.LogInformation("Returning default initrd script as node's reboot step index exceeds provision step count.");
                return defaultScript;
            }

            var serverContext = new IpxeProvisioningStepServerContext(request.RemoteAddress);

            var rebootStep = nodeProvisioningContext.RkmNodeGroupProvisioner.Spec!.Steps![rebootStepIndex];
            var provisioningRebootStep = _provisioningSteps[rebootStep!.Type];

            var overrideScriptText = await provisioningRebootStep.GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
                rebootStep!.DynamicSettings,
                node.Status,
                serverContext,
                cancellationToken);
            AutoexecScriptTemplate? overrideScript = null;
            if (overrideScriptText != null)
            {
                if (string.IsNullOrWhiteSpace(overrideScriptText) || !overrideScriptText.StartsWith("#!ipxe", StringComparison.Ordinal))
                {
                    _logger.LogWarning("Reboot script is not valid, ignoring!");
                    return new AutoexecScriptTemplate
                    {
                        ScriptTemplate =
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
                            """,
                        BootEventMessage = "Booting into default environment because the custom iPXE script is invalid"
                    };
                }

                overrideScript = new AutoexecScriptTemplate
                {
                    ScriptTemplate = overrideScriptText,
                    BootEventMessage = $"Booting to next iPXE script on reboot step {rebootStepIndex}"
                };
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
                if (node.Status.Provisioner.CurrentStepIndex >= nodeProvisioningContext.RkmNodeGroupProvisioner.Spec.Steps.Count)
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

                await request.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    node.Status.AttestationIdentityKeyFingerprint!,
                    $"Completed provisioning step '{rebootStep!.Type}' at index {rebootStepIndex}",
                    cancellationToken);
            }

            return overrideScript ?? defaultScript;
        }

        private async Task<string> GetAutoexecScript(
            UnauthenticatedFileTransferRequest request,
            bool skipDhcp,
            CancellationToken cancellationToken)
        {
            var node = await request.ConfigurationSource.GetRkmNodeByRegisteredIpAddressAsync(
                request.RemoteAddress.ToString(),
                cancellationToken);

            var dhcpCommand = "ifconf -c dhcp";
            if (!string.IsNullOrWhiteSpace(node?.Spec?.BootFromNetworkAdapter))
            {
                dhcpCommand = $"ifconf -c dhcp {node?.Spec?.BootFromNetworkAdapter}";
            }

            var selectedScript = await GetAutoexecScriptTemplate(
                request,
                node,
                cancellationToken);

            var bootedFromStepIndex = (node?.Status?.Provisioner?.RebootStepIndex ?? -1).ToString(CultureInfo.InvariantCulture);
            _logger.LogInformation($"Informing machine that they are booting from step index {bootedFromStepIndex}.");

            // This is a very limited subset of the substitutions done by the variable provider.
            var replacements = new Dictionary<string, string>
            {
                { "provision:bootedFromStepIndex", bootedFromStepIndex },
                { "provision:apiAddressIp", request.HttpContext!.Connection.LocalIpAddress.ToString() },
                { "step:dhcp", !skipDhcp ? dhcpCommand : string.Empty },
            };
            if (!string.IsNullOrWhiteSpace(node?.Status?.AttestationIdentityKeyFingerprint))
            {
                await request.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    node.Status.AttestationIdentityKeyFingerprint,
                    selectedScript.BootEventMessage,
                    cancellationToken);
            }
            var selectedScriptText = selectedScript.ScriptTemplate;
            foreach (var kv in replacements)
            {
                selectedScriptText = selectedScriptText.Replace("[[" + kv.Key + "]]", kv.Value, StringComparison.Ordinal);
            }
            return selectedScriptText;
        }
    }
}
