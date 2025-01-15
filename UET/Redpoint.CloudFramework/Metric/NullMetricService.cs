namespace Redpoint.CloudFramework.Metric
{
    using Google.Cloud.Datastore.V1;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class NullMetricService : IMetricService
    {
        public Task AddPoint(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null)
        {
            return Task.CompletedTask;
        }

        public void AddPointSync(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null)
        {
        }
    }
}
