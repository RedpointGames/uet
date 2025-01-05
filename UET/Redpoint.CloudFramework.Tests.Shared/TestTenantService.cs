namespace Redpoint.CloudFramework.Tests
{
    using Google.Cloud.Datastore.V1;
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

        public Task<Key?> GetTenantDatastoreKeyFromNamespace(string @namespace)
        {
            throw new NotImplementedException();
        }
    }
}
