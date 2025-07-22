namespace Redpoint.CloudFramework.Tests
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Datastore;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Tests.Models;
    using System.Threading.Tasks;
    using Xunit;

#pragma warning disable CS0618 // Type or member is obsolete

    [Collection("CloudFramework Test")]
    public class GlobalRepositoryTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public GlobalRepositoryTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        [Fact]
        public async Task TestLegacyHasAncestorQueryDoesNotCrash()
        {
            var repository = _env.Services.GetRequiredService<IGlobalRepository>();

            var key = await repository.CreateNamedKey<TestModel>(string.Empty, "blah").ConfigureAwait(true);

            var query = await repository.CreateQuery<TestModel>(string.Empty).ConfigureAwait(true);
            query.Query.Filter = Filter.HasAncestor(key);
            var results = await repository.RunUncachedQuery(string.Empty, query, readConsistency: ReadOptions.Types.ReadConsistency.Strong).ConfigureAwait(true);
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete
}
