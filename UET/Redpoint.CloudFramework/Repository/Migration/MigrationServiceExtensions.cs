namespace Redpoint.CloudFramework.Repository.Migration
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;

    public static class MigrationServiceExtensions
    {
        public static IServiceCollection AddMigration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TMigration>(this IServiceCollection services, int toSchemaVersion) where TModel : class, IModel, new() where TMigration : IModelMigrator<TModel>
        {
            services.AddTransient<RegisteredModelMigratorBase>(sp =>
            {
                return new RegisteredModelMigrator<TModel>
                {
                    MigratorType = typeof(TMigration),
                    ToSchemaVersion = toSchemaVersion,
                };
            });
            services.AddTransient(typeof(TMigration), typeof(TMigration));
            return services;
        }
    }
}
