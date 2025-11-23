namespace Redpoint.Uet.Database
{
    using Microsoft.Data.Sqlite;
    using Redpoint.Reservation;
    using Redpoint.Uet.Database.Migrations;
    using Redpoint.Uet.Database.Models;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    internal class DefaultUetDbConnection : IUetDbConnection
    {
        private readonly List<IMigration> _migrations;
        private readonly SqliteConnection _connection;

        public IReservation? Reservation;

        public DefaultUetDbConnection(
            IEnumerable<IMigration> migrations,
            string databasePath)
        {
            _migrations = migrations.ToList();
            _connection = new SqliteConnection($"Data Source={databasePath}");
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
            if (Reservation != null)
            {
                await Reservation.DisposeAsync();
            }
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await _connection.OpenAsync(cancellationToken);

            // Create the migrations table if it doesn't exist.
            {
                using var command = _connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS Migration
                    (
                        Key TEXT PRIMARY KEY
                    );
                    """;
                await command.ExecuteScalarAsync(cancellationToken);
            }

            // Iterate through each migration, check if it exists and if it doesn't, run it.
            foreach (var migration in _migrations)
            {
                await using var transaction = _connection.BeginTransaction(deferred: true);

                if (await FindAsync<Migration>(migration.Name, cancellationToken) == null)
                {
                    await migration.ExecuteAsync(_connection, cancellationToken);

                    await CreateAsync(
                        new Migration
                        {
                            Key = migration.Name
                        },
                        cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task CreateAsync<T>(
            T value,
            CancellationToken cancellationToken) where T : IUetModel, new()
        {
            ArgumentNullException.ThrowIfNull(value);

            using var command = _connection.CreateCommand();
            var columns = value.GetPropertyInfos().Select(x => x.Name).ToHashSet();
#pragma warning disable CA2100
            command.CommandText =
                $"""
                INSERT INTO {value.GetKind()}
                ({string.Join(", ", columns)})
                VALUES
                ({string.Join(", ", columns.Select(x => $"${x}"))});
                """;
#pragma warning restore CA2100
            foreach (var propInfo in value.GetPropertyInfos())
            {
                command.Parameters.AddWithValue($"${propInfo.Name}", propInfo.GetValue(value));
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task UpsertAsync<T>(
            T value,
            CancellationToken cancellationToken) where T : IUetModel, new()
        {
            ArgumentNullException.ThrowIfNull(value);

            using var command = _connection.CreateCommand();
            var columns = value.GetPropertyInfos().Select(x => x.Name).ToHashSet();
#pragma warning disable CA2100
            var upsertResolution = value.GetPropertyInfos().Length == 1
                ? "NOTHING"
                : $"""
                    UPDATE SET
                        {string.Join(
                            ", ",
                            columns
                                .Where(x => x != "Key")
                                .Select(x => $"{x} = ${x}"))}
                    """;
            command.CommandText =
                $"""
                INSERT INTO {value.GetKind()}
                ({string.Join(", ", columns)})
                VALUES
                ({string.Join(", ", columns.Select(x => $"${x}"))})
                ON CONFLICT(Key)
                DO {upsertResolution};
                """;
#pragma warning restore CA2100
            foreach (var propInfo in value.GetPropertyInfos())
            {
                command.Parameters.AddWithValue($"${propInfo.Name}", propInfo.GetValue(value));
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<T?> FindAsync<T>(
            string key,
            CancellationToken cancellationToken) where T : IUetModel, new()
        {
            ArgumentNullException.ThrowIfNull(key);

            var @ref = new T();

            using var command = _connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText =
                $"""
                SELECT *
                FROM {@ref.GetKind()}
                WHERE Key = $Key
                LIMIT 1;
                """;
#pragma warning restore CA2100
            command.Parameters.AddWithValue("Key", key);

            T? result = default;
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (result == null)
                {
                    result = new T();
                }
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var propInfo = result.GetPropertyInfo(reader.GetName(i));
                    if (propInfo == null)
                    {
                        continue;
                    }

                    if (propInfo.PropertyType == typeof(string))
                    {
                        propInfo.SetValue(result, reader.GetString(i));
                    }
                    else if (propInfo.PropertyType == typeof(double) || propInfo.PropertyType == typeof(double?))
                    {
                        propInfo.SetValue(result, reader.GetDouble(i));
                    }
                    else if (propInfo.PropertyType == typeof(long) || propInfo.PropertyType == typeof(long?))
                    {
                        propInfo.SetValue(result, reader.GetInt64(i));
                    }
                    else
                    {
                        // Ignored.
                    }
                }
            }
            return result;
        }

        public async IAsyncEnumerable<T> ListAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken) where T : IUetModel, new()
        {
            var @ref = new T();

            using var command = _connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText =
                $"""
                SELECT *
                FROM {@ref.GetKind()};
                """;
#pragma warning restore CA2100

            T? result = default;
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (result == null)
                {
                    result = new T();
                }
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var propInfo = result.GetPropertyInfo(reader.GetName(i));
                    if (propInfo == null)
                    {
                        continue;
                    }

                    if (propInfo.PropertyType == typeof(string))
                    {
                        propInfo.SetValue(result, reader.GetString(i));
                    }
                    else if (propInfo.PropertyType == typeof(double) || propInfo.PropertyType == typeof(double?))
                    {
                        propInfo.SetValue(result, reader.GetDouble(i));
                    }
                    else if (propInfo.PropertyType == typeof(long) || propInfo.PropertyType == typeof(long?))
                    {
                        propInfo.SetValue(result, reader.GetInt64(i));
                    }
                    else
                    {
                        // Ignored.
                    }
                }
                yield return result;
            }
        }
    }
}
