namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;

    internal interface IModelMigratorExecutor
    {
        Task ExecuteMigratorsAsync(
            RegisteredModelMigratorBase[] migrators,
            CancellationToken cancellationToken);
    }

    internal interface IModelMigratorExecutor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IModelMigratorExecutor where T : class, IModel, new()
    {
        Task IModelMigratorExecutor.ExecuteMigratorsAsync(
            RegisteredModelMigratorBase[] migrators,
            CancellationToken cancellationToken)
        {
            return ExecuteMigratorsAsync(
                migrators.Cast<RegisteredModelMigrator<T>>().ToArray(),
                cancellationToken);
        }

        Task ExecuteMigratorsAsync(
            RegisteredModelMigrator<T>[] migrators,
            CancellationToken cancellationToken);
    }
}
