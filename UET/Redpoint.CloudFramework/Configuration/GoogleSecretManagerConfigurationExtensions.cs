extern alias RDCommandLine;

using RDCommandLine::Microsoft.Extensions.Logging.Console;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.CloudFramework.GoogleInfrastructure;
using Redpoint.CloudFramework.Tracing;
using Redpoint.Logging.SingleLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Redpoint.CloudFramework.Configuration
{
    /// <summary>
    /// Extension methods for adding <see cref="SecretManagerConfigurationProvider"/>.
    /// </summary>
    public static class GoogleSecretManagerConfigurationExtensions
    {
        /// <summary>
        /// Adds the Google Cloud Secret Manager configuration source, passing the sources to <paramref name="addSourcesToConfiguration"/> for actual registration depending on the environment.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <param name="hostEnvironment">The host environment.</param>
        /// <param name="addSourcesToConfiguration">An action called to apply the configuration sources to the configuration builder.</param>
        /// <returns>The configuration builder.</returns>
        public static IConfigurationBuilder AddGoogleCloudSecretManager(
            this IConfigurationBuilder builder,
            IHostEnvironment hostEnvironment,
            Action<IConfigurationBuilder, IServiceProvider, IEnumerable<IConfigurationSource>> addSourcesToConfiguration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(hostEnvironment);
            ArgumentNullException.ThrowIfNull(addSourcesToConfiguration);

            // Construct our service provider and configuration source regardless
            // of whether we are in production to ensure that dependencies are satisifed.
            var minimalServices = new ServiceCollection();
            minimalServices.AddSingleton(hostEnvironment);
            minimalServices.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSingleLineConsoleFormatter(options =>
                {
                    options.OmitLogPrefix = false;
                    options.ColorBehavior = hostEnvironment.IsProduction() ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Default;
                });
                builder.AddSingleLineConsole();
            });
            minimalServices.AddSingleton<IGoogleServices, GoogleServices>();
            minimalServices.AddSingleton<IGoogleApiRetry, GoogleApiRetry>();
            minimalServices.AddSingleton<IManagedTracer, NullManagedTracer>();
            minimalServices.AddSecretManagerConfiguration(true);

            // @note: This service provider *MUST NOT* be disposed, as instances continue to use it
            // throughout the lifetime of the application, not just during configuration setup.
            var minimalServiceProvider = minimalServices.BuildServiceProvider();
            addSourcesToConfiguration(
                builder,
                minimalServiceProvider,
                minimalServiceProvider.GetServices<IConfigurationSource>());

            return builder;
        }

        /// <summary>
        /// Adds the Google Cloud Secret Manager configuration source, only if the application is running in production.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <param name="hostEnvironment">The host environment.</param>
        /// <returns>The configuration builder.</returns>
        public static IConfigurationBuilder AddGoogleCloudSecretManager(
            this IConfigurationBuilder builder,
            IHostEnvironment hostEnvironment)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            builder.AddGoogleCloudSecretManager(
                hostEnvironment,
                (builder, serviceProvider, sources) =>
                {
                    var minimalLogging = serviceProvider.GetRequiredService<ILogger<IConfigurationBuilder>>();
                    foreach (var configurationSource in sources)
                    {
                        if (hostEnvironment.IsProduction())
                        {
                            minimalLogging.LogInformation($"Adding '{configurationSource.GetType().FullName}' configuration source to configuration as this instance is running in production...");
                            builder.Add(configurationSource);
                        }
                        else
                        {
                            minimalLogging.LogInformation($"Not adding '{configurationSource.GetType().FullName}' configuration source to configuration as this instance is not running in production.");
                        }
                    }
                });

            return builder;
        }
    }
}
