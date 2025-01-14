﻿extern alias RDCommandLine;

namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.Prefix;
    using System;
    using System.Diagnostics.CodeAnalysis;

    public interface IBaseConfigurator<out TBase>
    {
        [Obsolete("Use AddPrefixProvider on the service collection inside Startup instead of this method.")]
        TBase UsePrefixProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : IPrefixProvider;

        TBase UseMultiTenant<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : ICurrentTenantService;

        /// <summary>
        /// Set what Google Cloud services are used at runtime. By default all services are on except for Logging, Trace and Error Reporting (which are sufficiently covered by Sentry instead).
        /// </summary>
        /// <param name="usageFlag"></param>
        /// <returns></returns>
        TBase UseGoogleCloud(GoogleCloudUsageFlag usageFlag);

        /// <summary>
        /// If called, this requires that the 'appsettings' secret be loaded from Google Cloud Secret Manager
        /// in production when the application starts up.
        /// 
        /// If your application relies on secrets from Google Cloud Secret Manager, you can use this to ensure
        /// the application doesn't start up in an inconsistent state.
        /// </summary>
        TBase RequireGoogleCloudSecretManagerConfiguration();

        TBase UseCustomConfigLayers(Action<IHostEnvironment, IConfigurationBuilder> customConfigLayers);
    }
}
