namespace Redpoint.Uet.Database.Migrations
{
    using Microsoft.Data.Sqlite;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class Migration001_LastEnginePath : IMigration
    {
        public string Name => "Migration001_LastEnginePath";

        public async Task ExecuteAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP TABLE IF EXISTS LastEnginePath;
                CREATE TABLE LastEnginePath
                (
                    Key TEXT PRIMARY KEY,
                    LastEnginePath TEXT
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
