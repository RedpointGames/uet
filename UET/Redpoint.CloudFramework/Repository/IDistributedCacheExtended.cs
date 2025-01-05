namespace Redpoint.CloudFramework.Repository
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDistributedCacheExtended
    {
        Task ClearAsync();
        Task<IEnumerable<string>> GetKeysAsync();
        Task RemoveAsync(string[] keys);
    }
}
