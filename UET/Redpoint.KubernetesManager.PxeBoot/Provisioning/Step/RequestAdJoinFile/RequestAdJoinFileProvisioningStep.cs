namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RequestAdJoinFile
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent;
    using Redpoint.KubernetesManager.PxeBoot.Variable;
    using Redpoint.RuntimeJson;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RequestAdJoinFileProvisioningStep : IProvisioningStep<RequestAdJoinFileProvisioningStepConfig>
    {
        private readonly IVariableProvider _variableProvider;
        private readonly ILogger<ModifyFilesProvisioningStep> _logger;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
        private readonly IDurableOperation _durableOperation;

        public RequestAdJoinFileProvisioningStep(
            IVariableProvider variableProvider,
            ILogger<ModifyFilesProvisioningStep> logger,
            ITpmSecuredHttp tpmSecuredHttp,
            IDurableOperation durableOperation)
        {
            _variableProvider = variableProvider;
            _logger = logger;
            _tpmSecuredHttp = tpmSecuredHttp;
            _durableOperation = durableOperation;
        }

        public string Type => "requestAdJoinFile";

        public IRuntimeJson GetJsonType(JsonSerializerOptions options)
        {
            return new ProvisioningStepConfigRuntimeJson(options).RequestAdJoinFileProvisioningStepConfig;
        }

        public ProvisioningStepFlags Flags => ProvisioningStepFlags.None;

        public Task ExecuteOnServerBeforeAsync(RequestAdJoinFileProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteOnClientAsync(RequestAdJoinFileProvisioningStepConfig config, IProvisioningStepClientContext context, CancellationToken cancellationToken)
        {
            var issuerAddress = _variableProvider.SubstituteVariables(context, config.ActiveDirectoryIssuerAddress);
            if (string.IsNullOrWhiteSpace(issuerAddress))
            {
                _logger.LogInformation("Skipping Active Directory join file fetch as no issuer address is set.");
                return;
            }

            var outputPath = _variableProvider.SubstituteVariables(context, config.OutputPath);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                _logger.LogInformation("Skipping Active Directory join file fetch as no output path is set.");
                return;
            }

            const int maxAttempts = 30;
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var clientFactory = await _tpmSecuredHttp.CreateHttpClientFactoryAsync(
                        new Uri($"http://{issuerAddress}:8792/negotiate"),
                        cancellationToken);
                    var client = clientFactory.Create();

                    var joinResponse = await _durableOperation.DurableOperationAsync(
                        async cancellationToken =>
                        {
                            return await client.PutAsJsonAsync(
                                new Uri($"https://{issuerAddress}:8793/get-join-file"),
                                new GetActiveDirectoryJoinBlobRequest
                                {
                                    NodeName = context.AuthorizedNodeName,
                                    AsUnattendXml = true,
                                },
                                ApiJsonSerializerContext.Default.GetActiveDirectoryJoinBlobRequest,
                                cancellationToken);
                        },
                        cancellationToken);
                    joinResponse.EnsureSuccessStatusCode();

                    var blobResponse = (await joinResponse.Content.ReadFromJsonAsync(
                        ApiJsonSerializerContext.Default.GetActiveDirectoryJoinBlobResponse,
                        cancellationToken))!;
                    await File.WriteAllTextAsync(
                        outputPath,
                        blobResponse.JoinBlob,
                        cancellationToken);

                    _logger.LogInformation("Successfully obtained Active Directory join file.");

                    return;
                }
                catch (Exception ex) when (i < maxAttempts - 1)
                {
                    _logger.LogError(ex, ex.Message);
                }
            }
        }

        public Task ExecuteOnServerAfterAsync(RequestAdJoinFileProvisioningStepConfig config, RkmNodeStatus nodeStatus, IProvisioningStepServerContext serverContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

    }
}
