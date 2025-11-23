namespace Redpoint.Uet.Database.Migrations
{
    using Microsoft.Data.Sqlite;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class Migration002_VerifiedDllFile : IMigration
    {
        public string Name => "Migration002_VerifiedDllFile";

        public async Task ExecuteAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP TABLE IF EXISTS VerifiedDllFile;
                CREATE TABLE VerifiedDllFile
                (
                    Key TEXT PRIMARY KEY,
                    LastWriteTime INTEGER
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
