namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
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

        public UploadFilesProvisioningStep(
            IFileTransferClient fileTransferClient,
            ILogger<UploadFilesProvisioningStep> logger,
            IDurableOperation durableOperation)
        {
            _fileTransferClient = fileTransferClient;
            _logger = logger;
            _durableOperation = durableOperation;
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
                if (file == null ||
                    string.IsNullOrWhiteSpace(file.Source) ||
                    !File.Exists(file.Source) ||
                    string.IsNullOrWhiteSpace(file.Target))
                {
                    throw new UnableToProvisionSystemException($"Skipping upload of '{file?.Source}', it may not exist on disk.");
                }

                _logger.LogInformation($"Uploading '{file.Source}' as '{file.Target}'...");
                await _durableOperation.DurableOperationAsync(
                    async cancellationToken =>
                    {
                        await _fileTransferClient.UploadFileAsync(
                            file.Source,
                            new Uri($"{context.ProvisioningApiEndpointHttps}/api/node-provisioning/upload-file?name={HttpUtility.UrlEncode(file.Target)}"),
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
