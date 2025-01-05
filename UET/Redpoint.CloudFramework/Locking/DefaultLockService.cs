namespace Redpoint.CloudFramework.Locking
{
    using System;
    using System.Threading.Tasks;
    using Google.Cloud.Datastore.V1;

    public class DefaultLockService : ILockService
    {
        private readonly ICurrentTenantService _currentTenantService;
        private readonly IGlobalLockService _globalLockService;

        public DefaultLockService(
            ICurrentTenantService currentTenantService,
            IGlobalLockService globalLockService)
        {
            _currentTenantService = currentTenantService;
            _globalLockService = globalLockService;
        }

        public async Task<ILockHandle> Acquire(Key objectToLock)
        {
            var currentTenant = await _currentTenantService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("ILockService can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return await _globalLockService.Acquire(ns, objectToLock).ConfigureAwait(false);
        }

        public async Task AcquireAndUse(Key objectToLock, Func<Task> block)
        {
            var currentTenant = await _currentTenantService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("ILockService can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            await _globalLockService.AcquireAndUse(ns, objectToLock, block).ConfigureAwait(false);
        }

        public async Task<T> AcquireAndUse<T>(Key objectToLock, Func<Task<T>> block)
        {
            var currentTenant = await _currentTenantService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("ILockService can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return await _globalLockService.AcquireAndUse(ns, objectToLock, block).ConfigureAwait(false);
        }
    }
}
