namespace Redpoint.KubernetesManager.PxeBoot.Variable
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using static System.CommandLine.Help.HelpBuilder;

    internal class DefaultVariableProvider : IVariableProvider
    {
        private readonly ILogger<DefaultVariableProvider> _logger;

        public DefaultVariableProvider(
            ILogger<DefaultVariableProvider> logger)
        {
            _logger = logger;
        }

        public Dictionary<string, string> ComputeParameterValuesNodeProvisioningEndpoint(
            ServerSideVariableContext context)
        {
            var serverOnlySubstitutions = new Dictionary<string, string>
            {
                { "provision:nodeName", context.RkmNode.Spec?.NodeName ?? string.Empty },
                { "provision:apiAddressIp", context.ApiHostAddress },
                { "provision:apiAddressHttp", $"http://{context.ApiHostAddress}:{context.ApiHostHttpPort}" },
                { "provision:aikFingerprint", context.RkmNode.Status?.AttestationIdentityKeyFingerprint ?? string.Empty },
            };
            string PerformServerSideSubstitutions(string content)
            {
                foreach (var substitution in serverOnlySubstitutions)
                {
                    content = content.Replace("[[" + substitution.Key + "]]", substitution.Value, StringComparison.Ordinal);
                }
                return content;
            }

            var parameterValues = new Dictionary<string, string>();
            if (context.RkmNodeProvisioner.Spec?.Parameters != null)
            {
                foreach (var defaultKv in context.RkmNodeProvisioner.Spec.Parameters)
                {
                    if (defaultKv.Value != null)
                    {
                        parameterValues[defaultKv.Key] = PerformServerSideSubstitutions(defaultKv.Value);
                    }
                    else
                    {
                        _logger.LogWarning($"Provisioner parameter '{defaultKv.Key}' is being ignored because it's value is null (you probably should have set it to an empty string instead).");
                    }
                }
            }
            if (context.RkmNodeGroup.Spec?.ProvisionerArguments != null)
            {
                foreach (var kv in context.RkmNodeGroup.Spec.ProvisionerArguments)
                {
                    // @note: You can only provide arguments for parameters that are actually defined.
                    // Parameters can have an empty string as a default value though.
                    if (parameterValues.ContainsKey(kv.Key))
                    {
                        if (kv.Value != null)
                        {
                            parameterValues[kv.Key] = PerformServerSideSubstitutions(kv.Value);
                        }
                        else
                        {
                            _logger.LogWarning($"Provisioner argument '{kv.Key}' is being ignored because it's value is null.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Provisioner argument '{kv.Key}' is being ignored because the provisioner does not define it as a parameter.");
                    }
                }
            }

            foreach (var parameterKv in parameterValues)
            {
                _logger.LogInformation($"Node '{context.RkmNode.Spec?.NodeName}' provisioner parameter '{parameterKv.Key}' evaluated as '{parameterKv.Value}'.");
            }

            return parameterValues;
        }

        private static Dictionary<string, string> GetSubstitutions(
            IProvisioningStepClientContext context,
            Dictionary<string, string>? stepValues)
        {
            var substitutions = new Dictionary<string, string>
            {
                { "provision:nodeName", context.AuthorizedNodeName },
                { "provision:apiAddressIp", context.ProvisioningApiAddress },
                { "provision:apiAddressHttp", context.ProvisioningApiEndpointHttp },
                { "provision:aikFingerprint", context.AikFingerprint },
            };
            if (stepValues != null)
            {
                foreach (var kv in stepValues)
                {
                    substitutions[$"step:{kv.Key}"] = kv.Value;
                }
            }
            if (context.Platform == ProvisioningClientPlatformType.Linux ||
                context.Platform == ProvisioningClientPlatformType.LinuxInitrd)
            {
                substitutions.Add("disk:path", context.DiskPathLinux ?? string.Empty);
                substitutions.Add("disk:partition:boot", $"{context.DiskPathLinux}-part{PartitionConstants.BootPartitionIndex}");
                substitutions.Add("disk:partition:provision", $"{context.DiskPathLinux}-part{PartitionConstants.ProvisionPartitionIndex}");
                substitutions.Add("disk:partition:os", $"{context.DiskPathLinux}-part{PartitionConstants.OperatingSystemPartitionIndex}");
                substitutions.Add("disk:partition:boot:number", PartitionConstants.BootPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("disk:partition:provision:number", PartitionConstants.ProvisionPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("disk:partition:os:number", PartitionConstants.OperatingSystemPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("mount:boot", MountConstants.LinuxBootMountPath);
                substitutions.Add("mount:provision", MountConstants.LinuxProvisionMountPath);
                substitutions.Add("mount:os", MountConstants.LinuxOperatingSystemMountPath);
                substitutions.Add("mount:ramdisk", MountConstants.LinuxRamdiskMountPath);
            }
            else if (context.Platform == ProvisioningClientPlatformType.Windows)
            {
                substitutions.Add("disk:number", "0");
                substitutions.Add("disk:partition:boot:number", PartitionConstants.BootPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("disk:partition:provision:number", PartitionConstants.ProvisionPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("disk:partition:os:number", PartitionConstants.OperatingSystemPartitionIndex.ToString(CultureInfo.InvariantCulture));
                substitutions.Add("mount:boot", MountConstants.WindowsBootDrive);
                substitutions.Add("mount:provision", MountConstants.WindowsProvisionDrive);
                substitutions.Add("mount:os", MountConstants.WindowsOperatingSystemDrive);
                substitutions.Add("mount:ramdisk", MountConstants.WindowsRamdiskDrive);
            }
            foreach (var kv in context.ParameterValues)
            {
                substitutions.Add($"param:{kv.Key}", kv.Value);
            }
            return substitutions;
        }

        public Dictionary<string, string> GetEnvironmentVariables(
            IProvisioningStepClientContext context,
            Dictionary<string, string>? stepValues = null)
        {
            var environmentVariables = new Dictionary<string, string>();

            foreach (var substitution in GetSubstitutions(context, stepValues))
            {
                var transformedKey = "RKM_";
                for (int i = 0; i < substitution.Key.Length; i++)
                {
                    var s = substitution.Key[i];
                    if (s >= 'A' && s <= 'Z' && i != 0)
                    {
                        transformedKey += "_";
                        transformedKey += s;
                    }
                    else if ((s >= '0' && s <= '9') || (s >= 'a' && s <= 'z'))
                    {
                        transformedKey += s;
                    }
                    else
                    {
                        transformedKey += "_";
                    }
                }
                transformedKey = transformedKey.ToUpperInvariant();
                _logger.LogInformation($"Parameter key transformed from '{substitution.Key}' to '{transformedKey}'.");
                environmentVariables.Add(transformedKey, substitution.Value);
            }

            return environmentVariables;
        }

        public string SubstituteVariables(
            IProvisioningStepClientContext context,
            string content,
            Dictionary<string, string>? stepValues = null)
        {
            foreach (var substitution in GetSubstitutions(context, stepValues))
            {
                // We intentionally use [[ instead of {{ so that .yaml files can be compatible with
                // both kubectl (which does not apply Go templates) and Helm (which does).
                content = content.Replace("[[" + substitution.Key + "]]", substitution.Value, StringComparison.Ordinal);
            }

            return content;
        }
    }
}
