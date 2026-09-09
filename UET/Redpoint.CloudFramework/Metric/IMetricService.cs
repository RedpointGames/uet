namespace Redpoint.CloudFramework.Metric
{
    using Redpoint.CloudFramework.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMetricService
    {
        Task AddPoint(string metricType, long amount, UntypedKey? projectKey, Dictionary<string, string?>? labels = null);

        void AddPointSync(string metricType, long amount, UntypedKey? projectKey, Dictionary<string, string?>? labels = null);
    }
}
