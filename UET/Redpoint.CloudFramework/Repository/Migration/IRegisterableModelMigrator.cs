namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "We require an empty base interface to share between model migrator interfaces for AddMigration implementation.")]
    public interface IRegisterableModelMigrator<T> where T : IModel
    {
    }
}
