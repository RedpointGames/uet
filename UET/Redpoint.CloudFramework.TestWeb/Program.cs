namespace Redpoint.CloudFramework.TestWeb
{
    using Redpoint.CloudFramework.Abstractions;
    using Redpoint.CloudFramework.Startup;

    public class Program : IWebAppProvider
    {
        public static async Task Main(string[] args)
        {
            Environment.SetEnvironmentVariable("REDPOINT_OTEL_USE_TELEMETRY_IN_DEVELOPMENT", "1");

            await CloudFramework.WebApp.StartWebApp(await GetHostAsync());
        }

        public static async ValueTask<ICloudFrameworkWebHost> GetHostAsync()
        {
            return await CloudFramework.WebApp
                .SetTraceSamplingRate(1.0)
                .UseStartup<Startup>()
                .UseGoogleCloud(GoogleCloudUsageFlag.None)
                .UseHelm((configuration, contentRootPath) =>
                {
                    return new HelmConfiguration
                    {
                        RedisPort = 32201,
                        DatastorePort = 32202,
                    };
                })
                .GetWebApp();
        }
    }
}
