using Redpoint.CloudFramework;
using Redpoint.CloudFramework.Startup;
using Redpoint.CloudFramework.TestService;

return await CloudFramework.ServiceApp
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