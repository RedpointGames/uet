namespace Redpoint.CloudFramework.BigQuery
{
    using Google.Cloud.BigQuery.V2;

    public class BigQueryResultsInfo
    {
        public required BigQueryResults Results { get; init; }

        public required BigQueryJob Job { get; init; }
    }
}
