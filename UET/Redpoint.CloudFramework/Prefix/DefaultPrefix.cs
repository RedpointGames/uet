namespace Redpoint.CloudFramework.Prefix
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    public class DefaultPrefix : IPrefix
    {
        private readonly ICurrentTenantService _currentProjectService;
        private readonly IGlobalPrefix _globalPrefix;

        public DefaultPrefix(ICurrentTenantService currentProjectService, IGlobalPrefix globalPrefix)
        {
            _currentProjectService = currentProjectService;
            _globalPrefix = globalPrefix;
        }

        public string Create(UntypedKey key)
        {
            return _globalPrefix.Create(key);
        }

        public string CreateInternal(UntypedKey key)
        {
            return _globalPrefix.CreateInternal(key);
        }

        public async Task<UntypedKey> Parse(string identifier)
        {
            var currentTenant = await _currentProjectService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IPrefix can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return _globalPrefix.Parse(ns, identifier);
        }

        public async Task<UntypedKey> ParseInternal(string identifier)
        {
            var currentTenant = await _currentProjectService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IPrefix can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return _globalPrefix.ParseInternal(ns, identifier);
        }

        public async Task<UntypedKey> ParseLimited(string identifier, string kind)
        {
            var currentTenant = await _currentProjectService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IPrefix can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return _globalPrefix.ParseLimited(ns, identifier, kind);
        }

        public async Task<Key<T>> ParseLimited<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string identifier) where T : class, IModel, new()
        {
            var currentTenant = await _currentProjectService.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IPrefix can not be used without a tenant.");
            }
            var ns = currentTenant.DatastoreNamespace;
            return _globalPrefix.ParseLimited<T>(ns, identifier);
        }
    }
}
