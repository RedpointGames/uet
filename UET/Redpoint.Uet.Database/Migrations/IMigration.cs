namespace Redpoint.Uet.Database.Migrations
{
    using Microsoft.Data.Sqlite;
    using System.Threading.Tasks;

    internal interface IMigration
    {
        string Name { get; }

        Task ExecuteAsync(SqliteConnection connection, CancellationToken cancellationToken);
    }
}
