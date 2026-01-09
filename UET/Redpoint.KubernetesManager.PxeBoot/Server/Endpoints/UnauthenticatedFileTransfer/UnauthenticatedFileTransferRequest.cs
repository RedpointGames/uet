namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Microsoft.AspNetCore.Http;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using System.Net;

    internal class UnauthenticatedFileTransferRequest
    {
        public required string PathPrefix { get; init; }

        public required PathString PathRemaining { get; init; }

        public required IPAddress RemoteAddress { get; init; }

        public required bool IsTftp { get; init; }

        public required IRkmConfigurationSource ConfigurationSource { get; init; }

        public required DirectoryInfo StaticFilesDirectory { get; init; }

        public required DirectoryInfo StorageFilesDirectory { get; init; }

        public required int HostHttpPort { get; init; }

        public required int HostHttpsPort { get; init; }

        public required KubernetesRkmJsonSerializerContext JsonSerializerContext { get; init; }

        public required HttpContext? HttpContext { get; init; }
    }
}
