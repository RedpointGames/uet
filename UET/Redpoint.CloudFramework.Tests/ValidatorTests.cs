using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Redpoint.CloudFramework.Repository.Converters.Model;
using Redpoint.CloudFramework.Repository.Converters.Timestamp;
using Redpoint.CloudFramework.Repository.Validation;
using Redpoint.CloudFramework.Tests.Models;
using Redpoint.CloudFramework.Tracing;
using Xunit;

namespace Redpoint.CloudFramework.Tests
{
    public class ValidatorTests
    {
        [Fact]
        public void TestValidation()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IModelValidator, DefaultModelValidator>();
            serviceCollection.AddSingleton<IModelConverter<string>, JsonModelConverter>();
            serviceCollection.AddSingleton<IModelConverter<Entity>, EntityModelConverter>();
            serviceCollection.AddCloudFrameworkCore();
            serviceCollection.AddCloudFrameworkGoogleCloud();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IHostEnvironment>(_ => new TestHostEnvironment());
            serviceCollection.AddSingleton<IManagedTracer, NullManagedTracer>();

            var services = serviceCollection.BuildServiceProvider();

            var validator = services.GetRequiredService<IModelValidator>();

            Assert.Throws<NotSupportedException>(() =>
            {
                validator.ValidateModelFields<BadModel>();
            });

            validator.ValidateModelFields<TestModel>();
        }

        private class TestHostEnvironment : IHostEnvironment
        {
            public string ApplicationName { get; set; } = null!;
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
            public string ContentRootPath { get; set; } = null!;
            public string EnvironmentName { get; set; } = Environments.Development;
        }
    }
}
