namespace Redpoint.CloudFramework.Metric
{
    using Google.Cloud.Datastore.V1;
    using Pipelines.Sockets.Unofficial.Threading;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.Concurrency;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    internal class DiagnosticSourceMetricService : IMetricService, IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "IMeterFactory disposes this automatically.")]
        private readonly Meter _meter;
        private readonly ConcurrentDictionary<string, Counter<long>> _counters;
        private readonly SemaphoreSlim _semaphore;
        private readonly IGlobalPrefix _globalPrefix;

        public DiagnosticSourceMetricService(
            IMeterFactory meterFactory,
            IGlobalPrefix globalPrefix)
        {
            _meter = meterFactory.Create("Redpoint.CloudFramework");
            _counters = new ConcurrentDictionary<string, Counter<long>>();
            _semaphore = new SemaphoreSlim(1);
            _globalPrefix = globalPrefix;
        }

        public Task AddPoint(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null)
        {
            AddPointSync(metricType, amount, projectKey, labels);
            return Task.CompletedTask;
        }

        public void AddPointSync(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null)
        {
            TagList tagList;
            tagList.Add("tenant_id", projectKey == null ? string.Empty : _globalPrefix.Create(projectKey));
            if (labels != null)
            {
                foreach (var kv in labels)
                {
                    tagList.Add(kv.Key, kv.Value);
                }
            }

            if (_counters.TryGetValue(metricType, out var existingCounter))
            {
                existingCounter.Add(amount, tagList);
                return;
            }

            _semaphore.Wait();
            try
            {
                var newOrExistingCounter = _counters.GetOrAdd(
                    metricType,
                    metricType => _meter.CreateCounter<long>(metricType));
                newOrExistingCounter.Add(amount, tagList);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            ((IDisposable)_semaphore).Dispose();
        }
    }
}
