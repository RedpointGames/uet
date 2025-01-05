namespace Redpoint.CloudFramework
{
    using Google.Cloud.Datastore.V1;
    using System.Threading.Tasks;

    public interface ICurrentTenantService
    {
        Task<ICurrentTenant?> GetTenant();

        Task<Key?> GetTenantDatastoreKeyFromNamespace(string @namespace);
    }

    public interface ICurrentTenant
    {
        string DatastoreNamespace { get; }
    }
}
