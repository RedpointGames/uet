namespace Redpoint.CloudFramework.BigQuery
{
    using System.Threading.Tasks;
    using Google.Apis.Bigquery.v2.Data;
    using Google.Cloud.BigQuery.V2;
    using Google.Cloud.Datastore.V1;

    public interface IBigQuery
    {
        Key PublicDatasetKey { get; }
        BigQueryClient GetBigQueryClient();
        void DeclareSchemaForTable(string table, int tableVersion, bool hasAutomaticExpiration, TableSchema schema);
        Task<BigQueryTable?> GetReadableTableForProject(Key projectKey, string table, int tableVersion);
        Task<BigQueryTable?> GetReadableTableForProject(string projectId, string table, int tableVersion);
        Task<BigQueryTable> GetWritableTableForProject(Key projectKey, string table, int tableVersion);
        Task<BigQueryTable> GetWritableTableForProject(string projectId, string table, int tableVersion);
        Task<BigQueryResultsInfo> ExecuteLegacyQuery(Key projectKey, string query);
        Task<BigQueryResultsInfo> ExecuteLegacyQuery(Key projectKey, string query, bool disableCache);
        Task<BigQueryResultsInfo> ExecuteStandardQuery(Key projectKey, string query, params BigQueryParameter[] parameters);
        Task<BigQueryResultsInfo> ExecuteStandardQuery(Key projectKey, string query, bool disableCache, params BigQueryParameter[] parameters);
        string EscapeLegacyString(string str);
        Task DeleteTableForProject(Key projectKey, string table, int tableVersion);
        Task<bool> GetTableExistsForProject(Key projectKey, string table, int? tableVersion);
        string GetDatasetNameForProject(Key projectKey);
        string GetTableNameFromTableAndVersion(string table, int tableVersion);
    }
}
