namespace Redpoint.CloudFramework
{
    using Redpoint.CloudFramework.Models;
    using System.Threading.Tasks;

    internal class SingleCurrentTenantService : ICurrentTenantService
    {
        public Task<ICurrentTenant?> GetTenant()
        {
            return Task.FromResult<ICurrentTenant?>(null);
        }

        public Task<UntypedKey?> GetTenantDatastoreKeyFromNamespace(string @namespace)
        {
            return Task.FromResult<UntypedKey?>(null);
        }
    }
}
