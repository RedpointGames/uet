namespace Redpoint.CloudFramework.Metric
{
    using Google.Cloud.Datastore.V1;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMetricService
    {
        Task AddPoint(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null);

        void AddPointSync(string metricType, long amount, Key? projectKey, Dictionary<string, string?>? labels = null);
    }
}
