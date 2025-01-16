namespace Redpoint.CloudFramework
{
    using System;

    /// <summary>
    /// Defines what Google Cloud services will be used at runtime. You can use this turn off certain services if you don't need them or otherwise have replacements.
    /// </summary>
    [Flags]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum GoogleCloudUsageFlag
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        /// <summary>
        /// Do not register any Google Cloud services.
        /// </summary>
        None = 0,

        /// <summary>
        /// Use everything.
        /// </summary>
        Default = All,

        /// <summary>
        /// Use Google Cloud Logging.
        /// </summary>
        [Obsolete("Google Cloud Logging support has been removed. Use Sentry instead.", true)]
        Logging = 1,

        /// <summary>
        /// Use Google Cloud Trace.
        /// </summary>
        [Obsolete("Google Cloud Trace support has been removed. Use Sentry instead.", true)]
        Trace = 2,

        /// <summary>
        /// Use Google Cloud Error Reporting.
        /// </summary>
        [Obsolete("Google Cloud Error Reporting support has been removed. Use Sentry instead.", true)]
        ErrorReporting = 4,

        /// <summary>
        /// Use Google Cloud Datastore.
        /// </summary>
        Datastore = 8,

        /// <summary>
        /// Use Google Cloud Pub/Sub.
        /// </summary>
        PubSub = 16,

        /// <summary>
        /// Use Google Cloud BigQuery.
        /// </summary>
        BigQuery = 32,

        /// <summary>
        /// Report metrics into Google Cloud Monitoring.
        /// </summary>
        [Obsolete("Google Cloud Monitoring support has been removed. Metrics are now reported via .NET meters.", true)]
        Metrics = 64,

        /// <summary>
        /// Load application configuration from Google Cloud Secret Manager.
        /// </summary>
        SecretManager = 128,

        /// <summary>
        /// Use all Google Cloud services that the framework uses.
        /// </summary>
#pragma warning disable CA1069 // Enums values should not be duplicated
        All = 255,
#pragma warning restore CA1069 // Enums values should not be duplicated
    }
}
