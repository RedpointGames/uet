namespace Redpoint.CloudFramework
{
    using Redpoint.CloudFramework.Models;
    using System.Threading.Tasks;

    public interface ICurrentTenantService
    {
        Task<ICurrentTenant?> GetTenant();

        Task<UntypedKey?> GetTenantDatastoreKeyFromNamespace(string @namespace);
    }

    public interface ICurrentTenant
    {
        string DatastoreNamespace { get; }
    }
}
