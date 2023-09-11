﻿namespace Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultStallDiagnostics : IStallDiagnostics
    {
        private readonly ILogger<DefaultStallDiagnostics> _logger;

        public DefaultStallDiagnostics(
            ILogger<DefaultStallDiagnostics> logger)
        {
            _logger = logger;
        }

        public async Task<string> CaptureStallInformationAsync(
            GraphExecutionInstance instance)
        {
            var workerPool = (DefaultTaskApiWorkerPool)instance.WorkerPool;
            var requestCollection = workerPool._requestCollection;
            var localWorkerFulfiller = workerPool._localWorkerFulfiller;
            var remoteWorkerFulfiller = workerPool._remoteWorkerFulfiller;

            _logger.LogInformation("Capturing stall diagnostics: Task statuses...");
            var statuses = await instance.GetTaskStatusesAsync().ConfigureAwait(false);

            _logger.LogInformation("Capturing stall diagnostics: Request collection...");
            WorkerCoreRequest<ITaskApiWorkerCore>[] requests;
            if (requestCollection != null)
            {
                requests = await requestCollection.GetAllRequestsAsync().ConfigureAwait(false);
            }
            else
            {
                requests = Array.Empty<WorkerCoreRequest<ITaskApiWorkerCore>>();
            }

            _logger.LogInformation("Capturing stall diagnostics: Local worker fulfiller...");
            SingleSourceWorkerCoreRequestFulfillerStatistics<ITaskApiWorkerCore>? localStatistics = null;
            if (localWorkerFulfiller != null)
            {
                localStatistics = localWorkerFulfiller.GetStatistics();
            }

            _logger.LogInformation("Capturing stall diagnostics: Remote worker fulfiller...");
            MultipleSourceWorkerCoreRequestFulfillerStatistics<ITaskApiWorkerCore>? remoteStatistics = null;
            if (remoteWorkerFulfiller != null)
            {
                remoteStatistics = remoteWorkerFulfiller.GetStatistics();
            }

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{statuses.Count} task statuses:");
            foreach (var status in statuses)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - {status.Key.GraphTaskSpec.Task.Name} = {status.Value}");
            }
            sb.AppendLine(CultureInfo.InvariantCulture, $"{requests.Length} requests:");
            foreach (var request in requests)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - requested = {request.DateRequestedUtc}, preference = {request.CorePreference}, assigned = {request.AssignedCore}");
            }
            if (localStatistics != null)
            {
                sb.AppendLine("local statistics:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{localStatistics.CoreAcquiringCount} cores being acquired");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{localStatistics.CoresCurrentlyAcquiredCount} cores currently acquired:");
                foreach (var core in localStatistics.CoresCurrentlyAcquired)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - {core}");
                }
            }
            else
            {
                sb.AppendLine("no local statistics available");
            }
            if (remoteStatistics != null)
            {
                sb.AppendLine("remote statistics:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{remoteStatistics.Providers.Count} providers connected");
                foreach (var kv in remoteStatistics.Providers)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - id = {kv.Key.Id}, unique id = {kv.Value.UniqueId}, is obtaining core = {kv.Value.IsObtainingCore}, obtained core number = {kv.Value.ObtainedCore?.WorkerCoreNumber}, obtained core machine name = {kv.Value.ObtainedCore?.WorkerMachineName}, obtained core unique assignment id = {kv.Value.ObtainedCore?.WorkerCoreUniqueAssignmentId}");
                }
            }
            else
            {
                sb.AppendLine("no remote statistics available");
            }

            return sb.ToString();
        }
    }
}