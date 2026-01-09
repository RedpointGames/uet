namespace Redpoint.KubernetesManager.PxeBoot.Server.Handlers
{
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal class DefaultPxeBootTftpRequestHandler : IPxeBootTftpRequestHandler
    {
        private readonly ILogger<DefaultPxeBootTftpRequestHandler> _logger;
        private readonly List<IUnauthenticatedFileTransferEndpoint> _endpoints;

        public DefaultPxeBootTftpRequestHandler(
            ILogger<DefaultPxeBootTftpRequestHandler> logger,
            IEnumerable<IUnauthenticatedFileTransferEndpoint> endpoints)
        {
            _logger = logger;
            _endpoints = endpoints.ToList();
        }

        public async Task HandleRequestAsync(
            PxeBootServerContext serverContext,
            ITftpTransfer transfer,
            EndPoint client)
        {
            try
            {
                var path = new PathString('/' + transfer.Filename.TrimStart('/'));
                var remoteAddress = ((IPEndPoint)client).Address;

                _logger.LogInformation($"TFTP: Incoming request from {remoteAddress} for '{path}'.");
                transfer.OnProgress.Add((args, _) =>
                {
                    _logger.LogTrace($"TFTP: Transfer progress: {args.Progress.TransferredBytes} / {args.Progress.TotalBytes}");
                    return Task.CompletedTask;
                });
                transfer.OnFinished.Add((args, _) =>
                {
                    _logger.LogInformation($"TFTP: Transfer finished.");
                    return Task.CompletedTask;
                });
                transfer.OnError.Add((args, _) =>
                {
                    _logger.LogInformation($"TFTP: Transfer error: {args.Error}");
                    return Task.CompletedTask;
                });

                foreach (var endpoint in _endpoints)
                {
                    foreach (var prefix in endpoint.Prefixes)
                    {
                        _logger.LogTrace($"TFTP: Checking request path '{path}' against '{prefix}'...");
                        if (path.StartsWithSegments(prefix, out var remaining))
                        {
                            _logger.LogTrace($"TFTP: Matched path against prefix, with remaining '{remaining}'.");
                            var request = new UnauthenticatedFileTransferRequest
                            {
                                PathPrefix = prefix,
                                PathRemaining = remaining,
                                RemoteAddress = remoteAddress,
                                IsTftp = true,
                                ConfigurationSource = serverContext.ConfigurationSource,
                                StaticFilesDirectory = serverContext.StaticFilesDirectory,
                                StorageFilesDirectory = serverContext.StorageFilesDirectory,
                                HostHttpPort = serverContext.HostHttpPort,
                                HostHttpsPort = serverContext.HostHttpsPort,
                                JsonSerializerContext = serverContext.JsonSerializerContext,
                                HttpContext = null,
                            };
                            try
                            {
                                var stream = await endpoint.GetDownloadStreamAsync(
                                    request,
                                    CancellationToken.None);
                                if (stream != null)
                                {
                                    _logger.LogInformation($"TFTP: Successfully returning stream for '{path}'.");
                                    transfer.Start(stream);
                                    return;
                                }
                            }
                            catch (DenyUnauthenticatedFileTransferException)
                            {
                                _logger.LogInformation($"TFTP: Explicitly denied access to path '{path}'.");
                                transfer.Cancel(TftpErrorPacket.AccessViolation);
                                return;
                            }
                        }
                    }
                }

                _logger.LogWarning($"TFTP: No result stream found for '{path}'.");
                transfer.Cancel(TftpErrorPacket.FileNotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                transfer.Cancel(TftpErrorPacket.IllegalOperation);
            }
        }
    }
}
