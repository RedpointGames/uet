namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using Redpoint.RuntimeJson;
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    internal class UploadFilesProvisioningStep : IProvisioningStep<UploadFilesProvisioningStepConfig>
    {
        private readonly IFileTransferClient _fileTransferClient;
        private readonly ILogger<UploadFilesProvisioningStep> _logger;
        private readonly IDurableOperation _durableOperation;
        private readonly IVariableProvider _variableProvider;

        public UploadFilesProvisioningStep(
            IFileTransferClient fileTransferClient,
            ILogger<UploadFilesProvisioningStep> logger,
            IDurableOperation durableOperation,
            IVariableProvider variableProvider)
        {
            _fileTransferClient = fileTransferClient;
            _logger = logger;
            _durableOperation = durableOperation;
            _variableProvider = variableProvider;
        }

        public string Type => "uploadFiles";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).UploadFilesProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(UploadFilesProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(UploadFilesProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Upload files step with {config.Files?.Count ?? 0} to consider.");
            foreach (var file in config.Files ?? [])
            {
                var source = _variableProvider.SubstituteVariables(context, file?.Source ?? string.Empty);
                var target = _variableProvider.SubstituteVariables(context, file?.Target ?? string.Empty);

                if (string.IsNullOrWhiteSpace(source) ||
                    !File.Exists(source) ||
                    string.IsNullOrWhiteSpace(target))
                {
                    throw new UnableToProvisionSystemException($"Skipping upload of '{file?.Source}', it may not exist on disk.");
                }

                _logger.LogInformation($"Uploading '{source}' as '{target}'...");
                await _durableOperation.DurableOperationAsync(
                    async cancellationToken =>
                    {
                        await _fileTransferClient.UploadFileAsync(
                            source,
                            new Uri($"{context.ProvisioningApiEndpointHttps}/api/node-provisioning/upload-file?name={HttpUtility.UrlEncode(target)}"),
                            context.ProvisioningApiClient,
                            cancellationToken);
                    },
                    cancellationToken);
            }
        }

        public Task ExecuteOnServerAfterAsync(UploadFilesProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

    }
}
