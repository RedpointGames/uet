using Redpoint.CloudFramework;
using Redpoint.CloudFramework.Startup;
using Redpoint.CloudFramework.TestService;

Environment.SetEnvironmentVariable("REDPOINT_OTEL_USE_TELEMETRY_IN_DEVELOPMENT", "1");

return await CloudFramework.ServiceApp
    .SetTraceSamplingRate(1.0)
    .UseDefaultRoles("test")
    .UseGoogleCloud(GoogleCloudUsageFlag.None)
    .AddContinuousProcessor<TestContinuousProcessor>()
    .UseHelm((_, _) =>
    {
        return new HelmConfiguration
        {
            RedisPort = 32201,
            DatastorePort = 32202,
        };
    })
    .StartServiceApp(args);