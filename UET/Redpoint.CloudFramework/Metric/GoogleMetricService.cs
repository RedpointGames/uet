namespace Redpoint.CloudFramework.Metric
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;
    using Google.Cloud.Monitoring.V3;
    using System.Collections.Generic;
    using Redpoint.CloudFramework.Prefix;
    using Google.Cloud.Datastore.V1;
    using System.Threading;
    using NodaTime;
    using System.Security.Cryptography;
    using System.Linq;
    using System.Text;
    using Google.Api.Gax.ResourceNames;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Microsoft.Extensions.Hosting;

    public sealed class GoogleMetricService : IMetricService, IAsyncDisposable
    {
        private readonly IGoogleServices _googleServices;
        private readonly ILogger<GoogleMetricService> _logger;
        private readonly IGlobalPrefix _globalPrefix;
        private readonly MetricServiceClient? _client;
        private readonly Task? _flushTask;
        private readonly Dictionary<string, TimeSeriesBuffer>? _buffer;
        private readonly SemaphoreSlim? _bufferSemaphore;

        public static CancellationTokenSource ProgramExitCancellationTokenSource { get; } = new CancellationTokenSource();

        public GoogleMetricService(
            IHostEnvironment hostEnvironment,
            IGoogleServices googleServices,
            ILogger<GoogleMetricService> logger,
            IGlobalPrefix globalPrefix)
        {
            ArgumentNullException.ThrowIfNull(googleServices);

            _googleServices = googleServices;
            _logger = logger;
            _globalPrefix = globalPrefix;

            if (hostEnvironment.IsDevelopment() || hostEnvironment.IsStaging())
            {
                return;
            }

            try
            {
                _client = googleServices.Build<MetricServiceClient, MetricServiceClientBuilder>(
                    MetricServiceClient.DefaultEndpoint,
                    MetricServiceClient.DefaultScopes);
            }
            catch (NotSupportedException)
            {
                // This environment might not support reporting metrics (for example, unit tests).
                _client = null;
            }

            if (_client != null)
            {
                _buffer = new Dictionary<string, TimeSeriesBuffer>();
                _bufferSemaphore = new SemaphoreSlim(1);
                _flushTask = Task.Run(BackgroundFlush);
            }
        }

        private class TimeSeriesBuffer
        {
            public TimeSeriesBuffer(
                Google.Api.Metric metric,
                Google.Api.MonitoredResource monitoredResource,
                long pointCount)
            {
                Metric = metric;
                MonitoredResource = monitoredResource;
                PointCount = pointCount;
            }

            public Google.Api.Metric Metric { get; }

            public Google.Api.MonitoredResource MonitoredResource { get; }

            public long PointCount { get; set; }
        }

        private async Task BackgroundFlush()
        {
            var token = ProgramExitCancellationTokenSource.Token;

            var projectName = new ProjectName(_googleServices.ProjectId);

            while (!token.IsCancellationRequested)
            {
                var endTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);

                await Task.Delay((int)Duration.FromMinutes(1).TotalMilliseconds, token).ConfigureAwait(false);

                await _bufferSemaphore!.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var kv in _buffer!.ToArray())
                    {
                        var timeSeriesData = new TimeSeries
                        {
                            Metric = kv.Value.Metric,
                            Resource = kv.Value.MonitoredResource,
                            MetricKind = Google.Api.MetricDescriptor.Types.MetricKind.Gauge,
                            ValueType = Google.Api.MetricDescriptor.Types.ValueType.Int64,
                            Points =
                            {
                                new Point
                                {
                                    Interval = new TimeInterval
                                    {
                                        EndTime = endTime,
                                    },
                                    Value = new TypedValue
                                    {
                                        Int64Value = kv.Value.PointCount,
                                    }
                                }
                            }
                        };

                        await _client!.CreateTimeSeriesAsync(new CreateTimeSeriesRequest
                        {
                            ProjectName = projectName,
                            TimeSeries =
                            {
                                timeSeriesData,
                            }
                        }).ConfigureAwait(false);

                        if (kv.Value.PointCount > 0)
                        {
                            // Reset the point count to zero so that if there's no data reported next time, we'll
                            // at least send a metric with a value of 0 so that graphs render correctly in
                            // Stackdriver.
                            kv.Value.PointCount = 0;
                        }
                        else
                        {
                            // The last sent metric had a value of zero, remove the entry from the buffer so we
                            // don't send metrics if we don't need (once it's reset to 0, we only need to notify
                            // Stackdriver again if it starts being non-zero).
                            _buffer!.Remove(kv.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                }
                finally
                {
                    _bufferSemaphore.Release();
                }
            }
        }

        private string ComputeHashKey(string metricType, Key? projectKey, Dictionary<string, string?>? labels)
        {
            var stringToHash = metricType + ":";
            if (projectKey == null)
            {
                stringToHash += "(global):";
            }
            else
            {
                stringToHash += _globalPrefix.Create(projectKey) + ":";
            }
            if (labels != null)
            {
                foreach (var key in labels.Keys.OrderBy(x => x))
                {
                    stringToHash += key + "=" + labels[key] + ":";
                }
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(stringToHash))).ToLowerInvariant();
        }

        public async Task AddPoint(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels)
        {
            if (_client == null)
            {
                // Environment does not support reporting metrics.
                return;
            }

            try
            {
                await _bufferSemaphore!.WaitAsync().ConfigureAwait(false);
                try
                {
                    var hashKey = ComputeHashKey(metricType, projectKey, labels);

                    if (!_buffer!.TryGetValue(hashKey, out TimeSeriesBuffer? timeSeriesBuffer))
                    {
                        var metric = new Google.Api.Metric
                        {
                            Type = "custom.googleapis.com/" + metricType,
                            Labels =
                            {
                                { "tenant_id", projectKey == null ? string.Empty : _globalPrefix.Create(projectKey) }
                            }
                        };
                        if (labels != null)
                        {
                            foreach (var kv in labels)
                            {
                                metric.Labels.Add(kv.Key, kv.Value);
                            }
                        };

                        var monitoredResource = new Google.Api.MonitoredResource
                        {
                            Type = "global",
                            Labels =
                            {
                                { "project_id", _googleServices.ProjectId }
                            }
                        };
                        timeSeriesBuffer = new TimeSeriesBuffer(
                            metric,
                            monitoredResource,
                            0);
                        _buffer.Add(hashKey, timeSeriesBuffer);
                    }

                    timeSeriesBuffer.PointCount += amount;
                }
                finally
                {
                    _bufferSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                // Log the error when trying to post the metric, but don't throw.
                _logger.LogError(new EventId(1), ex, ex.Message);
            }
        }

        public void AddPointSync(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null)
        {
            // Run on background thread, no need to synchronously wait for this to complete.
            Task.Run(async () =>
            {
                await AddPoint(metricType, amount, projectKey, labels).ConfigureAwait(false);
            });
        }

        public async ValueTask DisposeAsync()
        {
            ProgramExitCancellationTokenSource.Cancel();
            if (_flushTask != null)
            {
                try
                {
                    await _flushTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }
            _flushTask?.Dispose();
            _bufferSemaphore?.Dispose();
            ProgramExitCancellationTokenSource.Dispose();
        }
    }
}
