namespace Redpoint.CloudFramework.Repository.Migration
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;

    public static class MigrationServiceExtensions
    {
        public static IServiceCollection AddMigration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TMigrator>(this IServiceCollection services, int toSchemaVersion) where TModel : class, IModel, new() where TMigrator : IRegisterableModelMigrator<TModel>
        {
            services.AddTransient<RegisteredModelMigratorBase>(sp =>
            {
                return new RegisteredModelMigrator<TModel>
                {
                    MigratorType = typeof(TMigrator),
                    ToSchemaVersion = toSchemaVersion,
                };
            });
            services.AddTransient(typeof(TMigrator), typeof(TMigrator));
            services.AddTransient<IDesiredSchemaVersion<TMigrator>>(_ =>
            {
                return new DefaultDesiredSchemaVersion<TMigrator>(toSchemaVersion);
            });

            var executorInterfaceType = typeof(IModelMigratorExecutor<TModel>);
            var executorImplementationType = typeof(DefaultModelMigratorExecutor<TModel>);
            if (!services.Any(x => x.ServiceType == executorInterfaceType)) 
            {
                // Only register the executor once.
                services.AddTransient(executorInterfaceType, executorImplementationType);
            }

            return services;
        }
    }
}
