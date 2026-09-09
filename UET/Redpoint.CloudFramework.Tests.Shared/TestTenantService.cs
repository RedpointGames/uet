namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Threading.Tasks;

    internal class TestTenantService : ICurrentTenantService
    {
        private class TestTenant : ICurrentTenant
        {
            public string DatastoreNamespace => "test";
        }

        public Task<ICurrentTenant?> GetTenant()
        {
            return Task.FromResult<ICurrentTenant?>((ICurrentTenant)new TestTenant());
        }

        public Task<UntypedKey?> GetTenantDatastoreKeyFromNamespace(string @namespace)
        {
            throw new NotImplementedException();
        }
    }
}
