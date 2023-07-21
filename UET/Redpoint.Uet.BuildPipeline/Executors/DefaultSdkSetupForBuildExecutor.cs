namespace Redpoint.Uet.BuildPipeline.Executors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.SdkManagement;
    using System.Threading.Tasks;

    internal class DefaultSdkSetupForBuildExecutor : ISdkSetupForBuildExecutor
    {
        private readonly ILogger<DefaultSdkSetupForBuildExecutor> _logger;
        private readonly ILocalSdkManager _localSdkManager;

        public DefaultSdkSetupForBuildExecutor(
            ILogger<DefaultSdkSetupForBuildExecutor> logger,
            ILocalSdkManager localSdkManager)
        {
            _logger = logger;
            _localSdkManager = localSdkManager;
        }

        public async Task<Dictionary<string, string>> SetupForBuildAsync(
            BuildSpecification buildSpecification,
            string nodeName,
            string enginePath,
            Dictionary<string, string> inputEnvironmentVariables,
            CancellationToken cancellationToken)
        {
            var sdksPath = OperatingSystem.IsWindows() ? buildSpecification.BuildGraphEnvironment.Windows.SdksPath : buildSpecification.BuildGraphEnvironment.Mac?.SdksPath;
            Dictionary<string, string>? sdkEnvironment = null;
            if (sdksPath != null)
            {
                try
                {
                    _logger.LogInformation($"Setting up SDKs for BuildGraph node '{nodeName}'...");
                    sdkEnvironment = await _localSdkManager.SetupEnvironmentForBuildGraphNode(
                        enginePath,
                        sdksPath,
                        nodeName,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to automatically set up SDKs for BuildGraph node '{nodeName}', relying on global environment instead: {ex.Message}");
                }
            }
            else
            {
                _logger.LogTrace($"Automatic SDK setup is being skipped because there is no SDKs path configured.");
            }

            var globalEnvironmentVariablesWithSdk = inputEnvironmentVariables;
            if (sdkEnvironment != null)
            {
                globalEnvironmentVariablesWithSdk = new Dictionary<string, string>(inputEnvironmentVariables);
                foreach (var kv in sdkEnvironment)
                {
                    globalEnvironmentVariablesWithSdk[kv.Key] = kv.Value;
                }
            }

            return globalEnvironmentVariablesWithSdk;
        }
    }
}
