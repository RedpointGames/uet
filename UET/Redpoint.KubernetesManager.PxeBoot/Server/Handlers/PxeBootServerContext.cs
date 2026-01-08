namespace Redpoint.KubernetesManager.PxeBoot.Server.Handlers
{
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.Tpm;
    using System.Net;

    internal class PxeBootServerContext
    {
        public required IRkmConfigurationSource ConfigurationSource { get; init; }

        public required DirectoryInfo StaticFilesDirectory { get; init; }

        public required DirectoryInfo StorageFilesDirectory { get; init; }

        public required IPAddress HostAddress { get; init; }

        public required int HostHttpPort { get; init; }

        public required int HostHttpsPort { get; init; }

        public required KubernetesRkmJsonSerializerContext JsonSerializerContext { get; init; }

        public required Func<ITpmSecuredHttpServer> GetTpmSecuredHttpServer { get; init; }
    }
}
