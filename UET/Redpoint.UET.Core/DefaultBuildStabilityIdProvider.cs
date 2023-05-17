namespace Redpoint.UET.Core
{
    internal class DefaultBuildStabilityIdProvider : IBuildStabilityIdProvider
    {
        private readonly IStringUtilities _stringUtilities;

        public DefaultBuildStabilityIdProvider(IStringUtilities stringUtilities)
        {
            _stringUtilities = stringUtilities;
        }

        public string GetBuildStabilityId(string? workingDirectory, string? engine, string suffix)
        {
            var ci = Environment.GetEnvironmentVariable("CI");
            var redpointCiConcurrentId = Environment.GetEnvironmentVariable("REDPOINT_CI_CONCURRENT_ID");
            var ciRunnerId = Environment.GetEnvironmentVariable("CI_RUNNER_ID");
            var ciProjectId = Environment.GetEnvironmentVariable("CI_PROJECT_ID");
            var ciConcurrentProjectId = Environment.GetEnvironmentVariable("CI_CONCURRENT_PROJECT_ID");
            if (!string.IsNullOrWhiteSpace(redpointCiConcurrentId))
            {
                ciConcurrentProjectId = redpointCiConcurrentId;
            }

            if (ci == "true")
            {
                return _stringUtilities.GetStabilityHash($"{ciRunnerId}-{ciProjectId}-{ciConcurrentProjectId}-{engine}", 14) + $"-{suffix}";
            }
            else
            {
                return _stringUtilities.GetStabilityHash($"{workingDirectory}-{engine}", 14) + $"-{suffix}";
            }
        }
    }
}
