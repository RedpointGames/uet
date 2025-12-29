namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ModifyFilesProvisioningStep : IProvisioningStep<ModifyFilesProvisioningStepConfig>
    {
        private readonly IVariableProvider _variableProvider;
        private readonly ILogger<ModifyFilesProvisioningStep> _logger;

        public ModifyFilesProvisioningStep(
            IVariableProvider variableProvider,
            ILogger<ModifyFilesProvisioningStep> logger)
        {
            _variableProvider = variableProvider;
            _logger = logger;
        }

        public string Type => "modifyFiles";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).ModifyFilesProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(ModifyFilesProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(ModifyFilesProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            foreach (var file in config.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Path) ||
                    !Path.IsPathFullyQualified(file.Path))
                {
                    throw new UnableToProvisionSystemException($"Path '{file.Path}' is not fully qualified. Refusing to set file content.");
                }

                _logger.LogInformation($"Applying action '{file.Action}' to '{file.Path}'...");

                switch (file.Action)
                {
                    case ModifyFilesProvisioningStepConfigFileAction.CreateDirectory:
                        Directory.CreateDirectory(file.Path);
                        break;
                    case ModifyFilesProvisioningStepConfigFileAction.Delete:
                        if (Directory.Exists(file.Path))
                        {
                            await DirectoryAsync.DeleteAsync(file.Path, true);
                        }
                        else
                        {
                            File.Delete(file.Path);
                        }
                        break;
                    case ModifyFilesProvisioningStepConfigFileAction.SetContents:
                        {
                            var directoryName = Path.GetDirectoryName(file.Path);
                            if (!string.IsNullOrWhiteSpace(directoryName))
                            {
                                Directory.CreateDirectory(directoryName);
                            }

                            var content = file.Content;
                            if (file.EnableReplacements)
                            {
                                content = _variableProvider.SubstituteVariables(
                                    context,
                                    content);
                            }

                            await File.WriteAllTextAsync(
                                file.Path,
                                content,
                                cancellationToken);
                            break;
                        }
                }
            }
        }

        public Task ExecuteOnServerAfterAsync(ModifyFilesProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
