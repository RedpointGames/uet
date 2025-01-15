namespace Redpoint.CloudFramework
{
    using Google.Cloud.Datastore.V1;
    using System.Threading.Tasks;

    internal class SingleCurrentTenantService : ICurrentTenantService
    {
        public Task<ICurrentTenant?> GetTenant()
        {
            return Task.FromResult<ICurrentTenant?>(null);
        }

        public Task<Key?> GetTenantDatastoreKeyFromNamespace(string @namespace)
        {
            return Task.FromResult<Key?>(null);
        }
    }
}
